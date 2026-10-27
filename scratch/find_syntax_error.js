const fs = require('fs');

const html = fs.readFileSync('Views/Chat/Index.cshtml', 'utf8');
const scriptRegex = /<script\b[^>]*>([\s\S]*?)<\/script>/gi;
let match;
let scriptIndex = 0;

while ((match = scriptRegex.exec(html)) !== null) {
    scriptIndex++;
    if (scriptIndex === 2) {
        let code = match[1];

        // Simulate Razor engine rendering:
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

        let lines = rendered.split('\n');

        let low = 1, high = lines.length, errorLine = -1, lastErrMsg = '';
        while (low <= high) {
            let mid = Math.floor((low + high) / 2);
            let testChunk = lines.slice(0, mid).join('\n');
            try {
                new Function(testChunk);
                low = mid + 1;
            } catch (err) {
                if (err instanceof SyntaxError && (err.message.includes("Unexpected end of input") || err.message.includes("missing } after function body"))) {
                    low = mid + 1;
                } else {
                    errorLine = mid;
                    lastErrMsg = err.message;
                    high = mid - 1;
                }
            }
        }

        console.log("Error line:", errorLine, "Message:", lastErrMsg);
        if (errorLine > 0) {
            for (let j = Math.max(0, errorLine - 10); j <= Math.min(lines.length - 1, errorLine + 5); j++) {
                console.log(`${j+1}: ${lines[j]}`);
            }
        } else {
            try {
                new Function(rendered);
                console.log("WHOLE SCRIPT IS 100% VALID JAVASCRIPT!");
            } catch(e) {
                console.log("Final check error:", e);
            }
        }
    }
}
