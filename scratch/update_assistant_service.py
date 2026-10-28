with open('Models/Assistant/AssistantService.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target1 = """            var config = AgentConfigService.GetConfig();
            if (config.AgentStatus == "Disabled")
                return "Hệ thống AI Agent hiện đang bị tạm tắt bởi Quản trị viên.";
            if (config.AgentStatus == "Maintenance")
                return "Hệ thống AI Agent hiện đang trong chế độ bảo trì: " + config.StatusReason;"""

replacement1 = """            var config = AgentConfigService.GetConfig();
            if (config.AgentStatus == "Disabled")
                throw new InvalidOperationException("Hệ thống AI Agent hiện đang bị tạm tắt bởi Quản trị viên.");
            if (config.AgentStatus == "Maintenance")
                throw new InvalidOperationException("Hệ thống AI Agent hiện đang trong chế độ bảo trì: " + config.StatusReason);"""

target2 = """            return finalSummary;
        }

        /// <summary>
        /// Bộ điều phối thực thi tool an toàn: Kiểm tra quyền, Ghi nhật ký kiểm toán, và Xác nhận hành động nhạy cảm"""

replacement2 = """            if (actionsExecutedCount == 0 && string.IsNullOrWhiteSpace(lastTextResponse) && finalSummary == "Đã thực hiện tác vụ tự động.")
            {
                throw new InvalidOperationException("Mô hình AI không sinh ra hành động hoặc câu trả lời nào cho tác vụ này.");
            }

            return finalSummary;
        }

        /// <summary>
        /// Bộ điều phối thực thi tool an toàn: Kiểm tra quyền, Ghi nhật ký kiểm toán, và Xác nhận hành động nhạy cảm"""

text = text.replace('\r\n', '\n')
if target1 in text:
    text = text.replace(target1, replacement1)
    print("Target 1 replaced.")
else:
    print("Target 1 not found!")

if target2 in text:
    text = text.replace(target2, replacement2)
    print("Target 2 replaced.")
else:
    print("Target 2 not found!")

with open('Models/Assistant/AssistantService.cs', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated AssistantService.cs")
