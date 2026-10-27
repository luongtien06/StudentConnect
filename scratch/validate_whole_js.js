const fs = require('fs');

const html = fs.readFileSync('Views/Chat/Index.cshtml', 'utf8');
const scriptRegex = /<script\b[^>]*>([\s\S]*?)<\/script>/gi;
let match;
let scriptIndex = 0;

while ((match = scriptRegex.exec(html)) !== null) {
    scriptIndex++;
    if (scriptIndex === 2) {
        let code = match[1];

        let rendered = code
            .replace(/@\(isPrivate \? "true" : "false"\)/g, 'false')
            .replace(/@ViewBag\.[a-zA-Z0-9_]+/g, '1')
            .replace(/@\(Model\.Any\(\) \? Model\.Min\(m => m\.MessageID\) : 0\)/g, '0')
            .replace(/'@[^']*'/g, "'1'")
            .replace(/"@[^"]*"/g, '"1"')
            .replace(/@if\s*\([^)]*\)\s*\{[\s\S]*?\}|\s*@\{[\s\S]*?\}/g, (m) => {
                let textMatch = m.match(/<text>([\s\S]*?)<\/text>/i);
                if (textMatch) return textMatch[1];
                return '/* razor block */';
            })
            .replace(/<text>|<\/text>/g, '');

        try {
            new Function(rendered);
            console.log("SUCCESS! Script block #2 is 100% VALID JAVASCRIPT!");
        } catch(e) {
            console.error("Syntax Error on whole file:", e.message);
            console.error(e.stack);
        }
    }
}
