const fs = require('fs');

const html = fs.readFileSync('Views/Chat/Index.cshtml', 'utf8');
const scriptRegex = /<script\b[^>]*>([\s\S]*?)<\/script>/gi;
let match;
let scriptIndex = 0;

while ((match = scriptRegex.exec(html)) !== null) {
    scriptIndex++;
    if (scriptIndex === 2) {
        let code = match[1];

        // Replace all @(...) with 0 or string
        let rendered = code.replace(/@\(([\s\S]*?)\)/g, (m, expr) => {
            if (expr.includes('"')) return '""';
            return '0';
        });

        // Replace other razor items
        rendered = rendered
            .replace(/'@[^']*'/g, "'1'")
            .replace(/"@[^"]*"/g, '"1"')
            .replace(/@if\s*\([^)]*\)\s*\{[\s\S]*?\}|\s*@\{[\s\S]*?\}/g, (m) => {
                let textMatch = m.match(/<text>([\s\S]*?)<\/text>/i);
                if (textMatch) return textMatch[1];
                return '/* razor block */';
            })
            .replace(/<text>|<\/text>/g, '')
            .replace(/@ViewBag\.[a-zA-Z0-9_]+/g, '1')
            .replace(/@\w+/g, '0');

        fs.writeFileSync('scratch/rendered_chat.js', rendered, 'utf8');
        console.log("Wrote scratch/rendered_chat.js");
    }
}
