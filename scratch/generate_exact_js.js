const fs = require('fs');

const fullHtml = fs.readFileSync('Views/Chat/Index.cshtml', 'utf8');
const lines = fullHtml.split('\n');

// 2144 is <script> (1-indexed), so 2145 (index 2144) is first line of JS
// 3701 is </script> (1-indexed), so line before is 3700 (index 2144 to 3700)
let scriptLines = lines.slice(2144, 3700);

for (let i = 0; i < scriptLines.length; i++) {
    let l = scriptLines[i];
    if (l.includes("const myId")) scriptLines[i] = "    const myId = 1;";
    else if (l.includes("const roomId")) scriptLines[i] = "    const roomId = 1;";
    else if (l.includes("const isPrivatePage")) scriptLines[i] = "    const isPrivatePage = false;";
    else if (l.includes("let oldestMessageId")) scriptLines[i] = "    let oldestMessageId = 0;";
    else if (l.includes("let hasMoreHistory")) scriptLines[i] = "    let hasMoreHistory = true;";
    else if (l.includes("@if")) scriptLines[i] = "    if (false) {";
    else if (l.includes("<text>") || l.includes("</text>")) scriptLines[i] = "// " + l;
    else if (l.includes("@ViewBag")) scriptLines[i] = l.replace(/@ViewBag\.[a-zA-Z0-9_]+/g, '1');
}

fs.writeFileSync('scratch/exact_rendered_chat.js', scriptLines.join('\n'), 'utf8');
console.log("Wrote scratch/exact_rendered_chat.js. Total lines:", scriptLines.length);
