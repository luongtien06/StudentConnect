const fs = require('fs');

const fullHtml = fs.readFileSync('Views/Chat/Index.cshtml', 'utf8');
const lines = fullHtml.split('\n');

let scriptText = lines.slice(2144, 3700).join('\n');

scriptText = scriptText
    .replace(/const myId\s*=\s*parseInt\('[^']*'\);/, "const myId = 1;")
    .replace(/const roomId\s*=\s*parseInt\('[^']*'\);/, "const roomId = 1;")
    .replace(/const isPrivatePage\s*=[\s\S]*?;/, "const isPrivatePage = false;")
    .replace(/let oldestMessageId\s*=[\s\S]*?;/, "let oldestMessageId = 0;")
    .replace(/let hasMoreHistory\s*=[\s\S]*?;/, "let hasMoreHistory = true;")
    .replace(/@if\s*\(ViewBag\.WaitingForPair[\s\S]*?<\/text>\s*\}/, "/* waiting for pair block omitted when false */");

fs.writeFileSync('scratch/browser_rendered_chat.js', scriptText, 'utf8');
console.log("Wrote scratch/browser_rendered_chat.js");
