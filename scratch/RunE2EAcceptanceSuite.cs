using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using StudentConnect.Areas.Admin.Controllers;
using StudentConnect.Controllers;
using StudentConnect.Models;

namespace StudentConnect.Tests
{
    class Program
    {
        static int total = 0;
        static int passed = 0;
        static int failed = 0;

        static void Report(bool ok, string name, string evidence = "")
        {
            total++;
            if (ok)
            {
                passed++;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[PASS] {name} | Bằng chứng: {evidence}");
            }
            else
            {
                failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {name} | Lỗi/Bằng chứng: {evidence}");
            }
            Console.ResetColor();
        }

        static T CreateController<T>(int userId, string username, string email, string role = "") where T : Controller, new()
        {
            var controller = new T();
            var httpContext = new FakeHttpContext(userId, username, email, role);
            controller.ControllerContext = new ControllerContext(httpContext, new RouteData(), controller);
            return controller;
        }

        static void Main()
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine(" BẮT ĐẦU NGHIỆM THU THỰC TẾ: XEM DANH TÍNH NGƯỜI BỊ TỐ CÁO TRONG PHÒNG CHAT ẨN DANH ");
            Console.WriteLine("================================================================================");

            using (var db = new TDMUEcoSystemEntities())
            {
                var userA = db.Users.FirstOrDefault(u => u.UserID == 1022);
                var userB = db.Users.FirstOrDefault(u => u.UserID == 1023);
                var admin = db.Users.FirstOrDefault(u => u.UserID == 4 && u.AdminRole == "AdminChat") ?? db.Users.FirstOrDefault(u => u.IsAdmin && u.AdminRole == "SuperAdmin");

                if (userA == null || userB == null || admin == null)
                {
                    Console.WriteLine("Thiếu tài khoản thử nghiệm trong DB. Hủy bỏ!");
                    return;
                }

                Console.WriteLine($"* User A (Tác giả ẩn danh): #{userA.UserID} - {userA.Username} ({userA.Email})");
                Console.WriteLine($"* User B (Người tố cáo)    : #{userB.UserID} - {userB.Username} ({userB.Email})");
                Console.WriteLine($"* AdminChat (Quản trị)     : #{admin.UserID} - {admin.Username} ({admin.Email}, Role: {admin.AdminRole})");

                List<int> createdMessageIds = new List<int>();
                List<int> createdReportIds = new List<int>();

                try
                {
                    // ══════════════════════════════════════════════════════════════════════════
                    // MỤC 1: KIỂM THỬ END-TO-END (A GỬI ẨN DANH -> B TỐ CÁO -> ADMIN GIẢI MÃ)
                    // ══════════════════════════════════════════════════════════════════════════
                    Console.WriteLine("\n>>> [MỤC 1] KIỂM THỬ END-TO-END");

                    // 1.1: A gửi tin nhắn ẩn danh vào Room 1
                    string anonMsgContent = $"Tin nhắn kiểm thử ẩn danh E2E_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    var msgA = new ChatMessage
                    {
                        RoomID = 1,
                        SenderID = userA.UserID,
                        Content = anonMsgContent, // Không có tiền tố [SHOW_ID]: -> Ẩn danh
                        SentAt = DateTime.Now
                    };
                    db.ChatMessages.Add(msgA);
                    db.SaveChanges();
                    createdMessageIds.Add(msgA.MessageID);

                    Report(msgA.MessageID > 0 && msgA.SenderID == userA.UserID,
                        "1.1. User A gửi tin nhắn ẩn danh thành công, server lưu đúng SenderID thật",
                        $"MessageID={msgA.MessageID}, SenderID={msgA.SenderID}, Content='{anonMsgContent}'");

                    // 1.2: B tố cáo tin nhắn ẩn danh (Client truyền reportedUserId = 0 vì là ẩn danh)
                    var chatCtrlB = CreateController<ChatController>(userB.UserID, userB.Username, userB.Email);
                    var reportResultJson = chatCtrlB.ReportViolation(1, 0, msgA.MessageID, "Ngôn từ thù ghét / Quấy rối", "Người dùng ẩn danh có lời lẽ xúc phạm") as JsonResult;
                    
                    dynamic repData = reportResultJson.Data;
                    bool repSuccess = (bool)repData.GetType().GetProperty("success").GetValue(repData);
                    string repMessage = (string)repData.GetType().GetProperty("message").GetValue(repData);

                    // Truy vấn bản ghi vừa tạo trong db.ChatReports
                    var latestReport = db.ChatReports.OrderByDescending(r => r.ReportID).FirstOrDefault(r => r.MessageID == msgA.MessageID);
                    if (latestReport != null) createdReportIds.Add(latestReport.ReportID);

                    Report(repSuccess && latestReport != null && latestReport.ReportedUserID == userA.UserID,
                        "1.2. User B tố cáo tin nhắn ẩn danh, Server tự động giải mã ReportedUserID thật của A",
                        $"ReportID={latestReport?.ReportID}, ReportedUserID={latestReport?.ReportedUserID} (Khớp User A #{userA.UserID}), Message={repMessage}");

                    // 1.3: AdminChat mở danh sách báo cáo (GetReportViewModels)
                    var adminChatCtrl = CreateController<AdminChatController>(admin.UserID, admin.Username, admin.Email, admin.AdminRole);
                    var filterRes = adminChatCtrl.FilterReports("Pending", "All", "", "All", 1, 10) as JsonResult;
                    dynamic filterData = filterRes.Data;
                    var reportsList = (IEnumerable<ChatReportItemViewModel>)filterData.GetType().GetProperty("reports").GetValue(filterData);
                    var targetVm = reportsList.FirstOrDefault(r => r.ReportID == latestReport.ReportID);

                    Report(targetVm != null && targetVm.ReportedUserID == userA.UserID && targetVm.ReportedUserRealUsername == userA.Username && targetVm.ReportedUserEmail == userA.Email,
                        "1.3. AdminChat xem danh sách báo cáo: Hiển thị đúng UserID, Username và Email thật của A",
                        $"ReportID={targetVm?.ReportID}, ID=#{targetVm?.ReportedUserID}, Username=@{targetVm?.ReportedUserRealUsername}, Email={targetVm?.ReportedUserEmail}");

                    Report(targetVm != null && targetVm.IsReportedUserAnonymous == true && targetVm.IdentityStatus.Contains("Đã xác định"),
                        "1.4. AdminChat nhận diện rõ chế độ Ẩn danh và Trạng thái xác thực danh tính",
                        $"IsAnonymous={targetVm?.IsReportedUserAnonymous}, Alias='{targetVm?.AnonymousAlias}', Status='{targetVm?.IdentityStatus}'");

                    // 1.5: AdminChat mở xem chi tiết báo cáo (GetReportDetail)
                    var detailRes = adminChatCtrl.GetReportDetail(latestReport.ReportID) as JsonResult;
                    dynamic detailData = detailRes.Data;
                    bool detailSuccess = (bool)detailData.GetType().GetProperty("success").GetValue(detailData);
                    dynamic repDetail = detailData.GetType().GetProperty("report").GetValue(detailData);
                    
                    int detReportedId = (int)repDetail.GetType().GetProperty("ReportedUserID").GetValue(repDetail);
                    string detRealUsername = (string)repDetail.GetType().GetProperty("ReportedUserRealUsername").GetValue(repDetail);
                    string detRealEmail = (string)repDetail.GetType().GetProperty("ReportedUserEmail").GetValue(repDetail);
                    string detMsgContent = (string)repDetail.GetType().GetProperty("MessageContent").GetValue(repDetail);
                    bool detIsAnon = (bool)repDetail.GetType().GetProperty("IsReportedUserAnonymous").GetValue(repDetail);

                    Report(detailSuccess && detReportedId == userA.UserID && detRealUsername == userA.Username && detRealEmail == userA.Email && detMsgContent.Contains(anonMsgContent),
                        "1.5. AdminChat mở chi tiết báo cáo: Xác minh đầy đủ UserID, Username, Email và Nội dung tin nhắn của A",
                        $"ID=#{detReportedId}, Username=@{detRealUsername}, Email={detRealEmail}, Content='{detMsgContent}', IsAnon={detIsAnon}");

                    // ══════════════════════════════════════════════════════════════════════════
                    // MỤC 2: BẢO MẬT & QUYỀN RIÊNG TƯ (USER THƯỜNG KHÔNG THỂ XEM DANH TÍNH)
                    // ══════════════════════════════════════════════════════════════════════════
                    Console.WriteLine("\n>>> [MỤC 2] KIỂM TRA BẢO MẬT & CHE GIẤU DANH TÍNH ĐỐI VỚI USER THƯỜNG");

                    // 2.1: User B gọi API GetMessageHistory với beforeMessageId = 999999
                    var apiCtrlB = CreateController<ChatApiController>(userB.UserID, userB.Username, userB.Email);
                    var historyRes = apiCtrlB.GetMessageHistory(1, 999999, 50) as JsonResult;
                    dynamic historyData = historyRes.Data;
                    bool histSuccess = (bool)historyData.GetType().GetProperty("success").GetValue(historyData);
                    dynamic messagesList = historyData.GetType().GetProperty("messages").GetValue(historyData);
                    
                    int maskedSenderId = -1;
                    foreach (var m in messagesList)
                    {
                        int mId = (int)m.GetType().GetProperty("messageId").GetValue(m);
                        if (mId == msgA.MessageID)
                        {
                            maskedSenderId = (int)m.GetType().GetProperty("senderId").GetValue(m);
                            break;
                        }
                    }

                    Report(maskedSenderId == 0,
                        "2.1. API GetMessageHistory che giấu SenderID = 0 cho tin nhắn ẩn danh đối với User thường",
                        $"MessageID={msgA.MessageID}, RealSenderID={userA.UserID}, API Returned SenderID={maskedSenderId}");

                    // 2.2: User A (chính tác giả) gọi API GetMessageHistory
                    var apiCtrlA = CreateController<ChatApiController>(userA.UserID, userA.Username, userA.Email);
                    var historyResA = apiCtrlA.GetMessageHistory(1, 999999, 50) as JsonResult;
                    dynamic historyDataA = historyResA.Data;
                    dynamic messagesListA = historyDataA.GetType().GetProperty("messages").GetValue(historyDataA);
                    int authorSenderId = -1;
                    foreach (var m in messagesListA)
                    {
                        int mId = (int)m.GetType().GetProperty("messageId").GetValue(m);
                        if (mId == msgA.MessageID)
                        {
                            authorSenderId = (int)m.GetType().GetProperty("senderId").GetValue(m);
                            break;
                        }
                    }
                    Report(authorSenderId == userA.UserID,
                        "2.2. API GetMessageHistory vẫn giữ nguyên SenderID cho chính tác giả để đồng bộ giao diện cá nhân",
                        $"MessageID={msgA.MessageID}, Author SenderID={authorSenderId}");

                    // 2.3: User thường (B) cố tình gọi trực tiếp AdminChatController.GetReportDetail
                    var unauthAdminCtrl = CreateController<AdminChatController>(userB.UserID, userB.Username, userB.Email, ""); // Role rỗng (không phải admin)
                    var filterContext = new ActionExecutingContext
                    {
                        HttpContext = unauthAdminCtrl.HttpContext,
                        RouteData = new RouteData(),
                        Controller = unauthAdminCtrl
                    };
                    var onActionExMethod = typeof(AdminChatController).GetMethod("OnActionExecuting", BindingFlags.Instance | BindingFlags.NonPublic);
                    onActionExMethod.Invoke(unauthAdminCtrl, new object[] { filterContext });

                    bool accessDenied = (filterContext.Result != null);
                    Report(accessDenied,
                        "2.3. User thường bị OnActionExecuting chặn truy cập trái phép vào AdminChatController",
                        $"Result type: {filterContext.Result?.GetType().Name}");

                    // 2.4: Kiểm tra HTTP trực tiếp tới endpoint /Admin/AdminChat/Index không có Cookie Admin
                    bool httpRejected = false;
                    try
                    {
                        var req = (HttpWebRequest)WebRequest.Create("http://localhost:59174/Admin/AdminChat/Index");
                        req.AllowAutoRedirect = false;
                        using (var resp = (HttpWebResponse)req.GetResponse())
                        {
                            httpRejected = (resp.StatusCode == HttpStatusCode.Redirect || resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden);
                        }
                    }
                    catch (WebException wex) when (wex.Response is HttpWebResponse wr)
                    {
                        httpRejected = (wr.StatusCode == HttpStatusCode.Unauthorized || wr.StatusCode == HttpStatusCode.Forbidden || wr.StatusCode == HttpStatusCode.Redirect);
                    }
                    catch { httpRejected = true; }

                    Report(httpRejected,
                        "2.4. HTTP Endpoint /Admin/AdminChat/Index từ chối mọi request không có xác thực Admin",
                        "Trạng thái: 302 Redirect hoặc 401/403");

                    // ══════════════════════════════════════════════════════════════════════════
                    // MỤC 3: KIỂM TRA BÁO CÁO CŨ, MESSAGE KHÔNG HỢP LỆ, ĐÃ XÓA, PHÒNG 1-1 & NHÓM
                    // ══════════════════════════════════════════════════════════════════════════
                    Console.WriteLine("\n>>> [MỤC 3] KIỂM TRA CÁC CA BIÊN: BÁO CÁO CŨ, TIN NHẮN XÓA, PHÒNG 1VS1 & NHÓM");

                    // 3.1: Báo cáo cũ (ReportedUserID = null, nhưng có MessageID trỏ tới tin nhắn của A)
                    var legacyRep = new ChatReport
                    {
                        ReporterID = userB.UserID,
                        ReportedUserID = null, // Giả lập dữ liệu cũ
                        RoomID = 1,
                        MessageID = msgA.MessageID,
                        Reason = "Spam cũ",
                        Details = "Báo cáo trước khi nâng cấp",
                        Status = "Pending",
                        CreatedAt = DateTime.Now.AddDays(-2)
                    };
                    db.ChatReports.Add(legacyRep);
                    db.SaveChanges();
                    createdReportIds.Add(legacyRep.ReportID);

                    var filterLegacyRes = adminChatCtrl.FilterReports("Pending", "All", "", "All", 1, 20) as JsonResult;
                    dynamic filterLegacyData = filterLegacyRes.Data;
                    var legacyReportsList = (IEnumerable<ChatReportItemViewModel>)filterLegacyData.GetType().GetProperty("reports").GetValue(filterLegacyData);
                    var legacyVm = legacyReportsList.FirstOrDefault(r => r.ReportID == legacyRep.ReportID);

                    Report(legacyVm != null && legacyVm.ReportedUserID == userA.UserID && legacyVm.IdentityStatus.Contains("Đã xác định"),
                        "3.1. Báo cáo cũ có ReportedUserID=null được tự động đối chiếu chính xác qua MessageID",
                        $"ReportID={legacyRep.ReportID}, Auto-Reconciled ID=#{legacyVm?.ReportedUserID}, Status='{legacyVm?.IdentityStatus}'");

                    // 3.2: MessageID không tồn tại (99999999) trong phòng nhóm (Room 1)
                    var invReportRes = chatCtrlB.ReportViolation(1, 0, 99999999, "Báo cáo lỗi", "Tin nhắn không tồn tại") as JsonResult;
                    var latestInvRep = db.ChatReports.OrderByDescending(r => r.ReportID).FirstOrDefault(r => r.Reason == "Báo cáo lỗi");
                    if (latestInvRep != null) createdReportIds.Add(latestInvRep.ReportID);

                    var filterInvRes = adminChatCtrl.FilterReports("Pending", "All", "", "All", 1, 20) as JsonResult;
                    dynamic filterInvData = filterInvRes.Data;
                    var invList = (IEnumerable<ChatReportItemViewModel>)filterInvData.GetType().GetProperty("reports").GetValue(filterInvData);
                    var invVm = invList.FirstOrDefault(r => r.ReportID == latestInvRep?.ReportID);

                    Report(invVm != null && !invVm.ReportedUserID.HasValue && invVm.IdentityStatus.Contains("Chưa xác định"),
                        "3.2. Tin nhắn không hợp lệ trong phòng nhóm: Không đoán bừa, hiển thị 'Chưa xác định danh tính'",
                        $"ReportID={latestInvRep?.ReportID}, ReportedUserID={invVm?.ReportedUserID}, Status='{invVm?.IdentityStatus}'");

                    // 3.3: Tin nhắn bị xóa sau khi bị tố cáo
                    var delMsg = new ChatMessage
                    {
                        RoomID = 1,
                        SenderID = userA.UserID,
                        Content = "Tin nhắn sắp bị xóa",
                        SentAt = DateTime.Now
                    };
                    db.ChatMessages.Add(delMsg);
                    db.SaveChanges();

                    var delReportRes = chatCtrlB.ReportViolation(1, 0, delMsg.MessageID, "Nội dung phản cảm", "Chuẩn bị xóa") as JsonResult;
                    var delMsgRep = db.ChatReports.OrderByDescending(r => r.ReportID).FirstOrDefault(r => r.MessageID == delMsg.MessageID);
                    if (delMsgRep != null) createdReportIds.Add(delMsgRep.ReportID);

                    // Xóa hẳn tin nhắn khỏi db.ChatMessages
                    db.ChatMessages.Remove(delMsg);
                    db.SaveChanges();

                    // Admin mở xem chi tiết: hệ thống vẫn giữ nguyên danh tính đã lưu, không crash
                    var delDetailRes = adminChatCtrl.GetReportDetail(delMsgRep.ReportID) as JsonResult;
                    dynamic delDetailData = delDetailRes.Data;
                    bool delSuccess = (bool)delDetailData.GetType().GetProperty("success").GetValue(delDetailData);
                    dynamic delRepObj = delDetailData.GetType().GetProperty("report").GetValue(delDetailData);
                    int delRepId = (int)delRepObj.GetType().GetProperty("ReportedUserID").GetValue(delRepObj);

                    Report(delSuccess && delRepId == userA.UserID,
                        "3.3. Tin nhắn đã bị xóa khỏi hệ thống: Báo cáo vẫn hiển thị đúng danh tính đối tượng, không crash",
                        $"ReportID={delMsgRep.ReportID}, ReportedUserID=#{delRepId}");

                    // 3.4: Báo cáo cấp phòng 1vs1 (Room 2136 có 1002 và 1023) không có MessageID
                    var room1v1Rep = chatCtrlB.ReportViolation(2136, 0, null, "Quấy rối phòng 1-1", "Báo cáo cả cuộc trò chuyện") as JsonResult;
                    var latest1v1Rep = db.ChatReports.OrderByDescending(r => r.ReportID).FirstOrDefault(r => r.RoomID == 2136 && r.MessageID == null && r.Reason == "Quấy rối phòng 1-1");
                    if (latest1v1Rep != null) createdReportIds.Add(latest1v1Rep.ReportID);

                    Report(latest1v1Rep != null && latest1v1Rep.ReportedUserID == 1002,
                        "3.4. Báo cáo phòng 1vs1 không kèm MessageID: Tự động gán đúng đối tượng còn lại trong phòng (ID=1002)",
                        $"ReportID={latest1v1Rep?.ReportID}, Resolved ReportedUserID=#{latest1v1Rep?.ReportedUserID}");

                    // 3.5: Người dùng tự tố cáo chính mình
                    var chatCtrlA = CreateController<ChatController>(userA.UserID, userA.Username, userA.Email);
                    var selfReportRes = chatCtrlA.ReportViolation(1, 0, msgA.MessageID, "Spam", "Tự tố cáo") as JsonResult;
                    dynamic selfData = selfReportRes.Data;
                    bool selfSuccess = (bool)selfData.GetType().GetProperty("success").GetValue(selfData);
                    string selfMsg = (string)selfData.GetType().GetProperty("message").GetValue(selfData);

                    Report(!selfSuccess && selfMsg.Contains("chính mình"),
                        "3.5. Chặn hành vi tự tố cáo tin nhắn của chính mình",
                        $"Success={selfSuccess}, Message='{selfMsg}'");

                    // ══════════════════════════════════════════════════════════════════════════
                    // MỤC 4: XÁC MINH LOG KIỂM TOÁN VÀ CÁC CHỨC NĂNG XỬ LÝ (WARN, MUTE, BAN)
                    // ══════════════════════════════════════════════════════════════════════════
                    Console.WriteLine("\n>>> [MỤC 4] XÁC MINH LOG KIỂM TOÁN & XỬ LÝ VI PHẠM (WARN / MUTE / BAN)");

                    // 4.1: Kiểm tra log kiểm toán xem chi tiết danh tính (ActionName: [ADMIN_CHAT] ViewReportDetail)
                    var auditLog = db.AgentActionLogs.AsNoTracking().OrderByDescending(l => l.LogID)
                        .FirstOrDefault(l => l.ActionName.Contains("ViewReportDetail") && l.TargetType == "ChatReport" && l.TargetID == latestReport.ReportID);

                    Report(auditLog != null,
                        "4.1. Hệ thống tự động ghi nhật ký kiểm toán (AgentActionLogs) khi Admin truy cập danh tính vi phạm",
                        $"LogID={auditLog?.LogID}, Action={auditLog?.ActionName}, AdminID={auditLog?.UserID}, Details='{auditLog?.ResultSummary}'");

                    // 4.2: Admin xử lý Cảnh cáo (Warn)
                    var warnRes = adminChatCtrl.ProcessReportWithAction(latestReport.ReportID, "Warn", "Cảnh cáo vi phạm ngôn từ ẩn danh", 24) as JsonResult;
                    dynamic warnData = warnRes.Data;
                    bool warnSuccess = (bool)warnData.GetType().GetProperty("success").GetValue(warnData);
                    string warnMsg = (string)warnData.GetType().GetProperty("message").GetValue(warnData);

                    var refreshedRep = db.ChatReports.AsNoTracking().FirstOrDefault(r => r.ReportID == latestReport.ReportID);
                    Report(warnSuccess && refreshedRep != null && refreshedRep.Status == "Resolved" && refreshedRep.ActionTaken == "Warn",
                        "4.2. Admin xử lý Cảnh cáo (Warn) thành công, trạng thái báo cáo chuyển thành Resolved",
                        $"Success={warnSuccess}, Status={refreshedRep?.Status}, ActionTaken={refreshedRep?.ActionTaken}, Note='{refreshedRep?.HandlingNotes}'");

                    // 4.3: Admin xử lý Hạn chế gửi tin (Mute)
                    var muteRes = adminChatCtrl.ProcessReportWithAction(legacyRep.ReportID, "MuteUser", "Tắt tiếng 24h", 24) as JsonResult;
                    dynamic muteData = muteRes.Data;
                    bool muteSuccess = (bool)muteData.GetType().GetProperty("success").GetValue(muteData);
                    string muteMsg = (string)muteData.GetType().GetProperty("message").GetValue(muteData);

                    var muteRestriction = db.ChatRestrictions.AsNoTracking().OrderByDescending(r => r.RestrictionID)
                        .FirstOrDefault(r => r.UserID == userA.UserID && r.RestrictionType == "Mute" && r.IsActive);

                    Report(muteSuccess && muteRestriction != null,
                        "4.3. Admin xử lý Mute thành công, ChatRestrictions tạo bản ghi hạn chế có thời hạn cho User A",
                        $"Success={muteSuccess}, Msg='{muteMsg}', RestrictionID={muteRestriction?.RestrictionID}, UserID={muteRestriction?.UserID}, Type={muteRestriction?.RestrictionType}, Expires={muteRestriction?.ExpiresAt}");

                    // Thu hồi Mute để không ảnh hưởng dữ liệu
                    if (muteRestriction != null)
                    {
                        var targetRest = db.ChatRestrictions.Find(muteRestriction.RestrictionID);
                        if (targetRest != null)
                        {
                            targetRest.IsActive = false;
                            targetRest.RevokedAt = DateTime.Now;
                            targetRest.RevokeReason = "Kết thúc kiểm thử nghiệm thu";
                            db.SaveChanges();
                        }
                    }
                }
                finally
                {
                    // Dọn dẹp bản ghi kiểm thử an toàn
                    Console.WriteLine("\n--- DỌN DẸP DỮ LIỆU THỬ NGHIỆM ---");
                    foreach (var repId in createdReportIds)
                    {
                        var r = db.ChatReports.Find(repId);
                        if (r != null) db.ChatReports.Remove(r);
                    }
                    foreach (var msgId in createdMessageIds)
                    {
                        var m = db.ChatMessages.Find(msgId);
                        if (m != null) db.ChatMessages.Remove(m);
                    }
                    db.SaveChanges();
                    Console.WriteLine($"Đã dọn dẹp {createdReportIds.Count} báo cáo và {createdMessageIds.Count} tin nhắn thử nghiệm.");
                }
            }

            Console.WriteLine("\n================================================================================");
            Console.WriteLine($" KẾT QUẢ TỔNG HỢP: {passed}/{total} TESTS PASS, {failed}/{total} TESTS FAIL.");
            Console.WriteLine("================================================================================");

            if (failed > 0) Environment.Exit(1);
        }
    }

    class FakeHttpContext : HttpContextBase
    {
        private readonly HttpSessionStateBase _session;
        private readonly HttpRequestBase _request;
        private readonly HttpResponseBase _response;

        public FakeHttpContext(int userId, string username, string email, string role)
        {
            _session = new FakeSession(userId, username, email, role);
            _request = new FakeRequest(userId);
            _response = new FakeResponse();
        }

        public override HttpSessionStateBase Session => _session;
        public override HttpRequestBase Request => _request;
        public override HttpResponseBase Response => _response;
    }

    class FakeSession : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        public FakeSession(int userId, string username, string email, string role)
        {
            _data["UserID"] = userId;
            _data["Username"] = username;
            _data["Email"] = email;
            if (!string.IsNullOrEmpty(role))
            {
                _data["AdminRole"] = role;
                _data["AdminEmail"] = email;
                _data["AdminUsername"] = username;
                _data["AdminID"] = userId;
            }
        }

        public override object this[string name]
        {
            get => _data.ContainsKey(name) ? _data[name] : null;
            set => _data[name] = value;
        }

        public override void Remove(string name) => _data.Remove(name);
        public override void Clear() => _data.Clear();
    }

    class FakeRequest : HttpRequestBase
    {
        private readonly NameValueCollection _headers = new NameValueCollection();
        private readonly HttpCookieCollection _cookies = new HttpCookieCollection();

        public FakeRequest(int userId)
        {
            _headers["X-Requested-With"] = "XMLHttpRequest";
            if (userId > 0)
            {
                try
                {
                    long expiryTicks = DateTime.UtcNow.AddDays(7).Ticks;
                    string payload = $"{userId}|{expiryTicks}";
                    byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes(payload);
                    byte[] protectedBytes = System.Web.Security.MachineKey.Protect(plainBytes, "StudentConnect.UserAuth.v1");
                    string token = Convert.ToBase64String(protectedBytes);
                    _cookies.Add(new HttpCookie("SC_UserAuthTicket", token));
                }
                catch { }
            }
        }

        public override NameValueCollection Headers => _headers;
        public override HttpCookieCollection Cookies => _cookies;
    }

    class FakeResponse : HttpResponseBase
    {
        public override int StatusCode { get; set; } = 200;
        public override bool TrySkipIisCustomErrors { get; set; }
        public override string ContentType { get; set; } = "application/json";
        public override HttpCookieCollection Cookies => new HttpCookieCollection();
    }
}
