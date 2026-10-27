const fs = require('fs');

const html = fs.readFileSync('Views/Chat/Index.cshtml', 'utf8');
const scriptRegex = /<script\b[^>]*>([\s\S]*?)<\/script>/gi;
let match;
let scriptIndex = 0;

while ((match = scriptRegex.exec(html)) !== null) {
    scriptIndex++;
    if (scriptIndex === 2) {
        let lines = match[1].split('\n');
        console.log(`Script block #2 total lines: ${lines.length}`);
        for (let i = 1545; i < 1565 && i < lines.length; i++) {
            console.log(`${i+1}: ${lines[i]}`);
        }
    }
}
