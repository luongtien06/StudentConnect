@echo off
title TDMU StudentConnect - AI Agent Mr. T Independent Worker
color 0B
echo =====================================================================
echo  KHOI DONG WORKER DOC LAP - AI AGENT MR. T (TDMU STUDENTCONNECT)
echo =====================================================================
echo  Worker nay chay ngam doc lap voi trinh duyet web va IIS.
echo  Moi tac vu tu dong den han se duoc kiem tra va thuc thi dinh ky.
echo  Nhan Ctrl+C de dung worker.
echo =====================================================================
echo.
"%~dp0StudentConnect.Worker\bin\Debug\StudentConnect.Worker.exe"
pause
