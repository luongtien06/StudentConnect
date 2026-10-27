const fs = require('fs');

const html = fs.readFileSync('Views/Chat/Index.cshtml', 'utf8');
const scriptRegex = /<script\b[^>]*>([\s\S]*?)<\/script>/gi;
let match;
let scriptIndex = 0;

while ((match = scriptRegex.exec(html)) !== null) {
    scriptIndex++;
    if (scriptIndex === 2) {
        let lines = match[1].split('\n');
        let braceDepth = 0;
        let parenDepth = 0;

        for (let i = 0; i < lines.length; i++) {
            let line = lines[i];
            // Remove string literals and comments to count brackets accurately
            let cleaned = line
                .replace(/'(\\.|[^'])*'/g, "''")
                .replace(/"(\\.|[^"])*"/g, '""')
                .replace(/`(\\.|[^`])*`/g, '``')
                .replace(/\/\/.*$/, '');

            for (let c of cleaned) {
                if (c === '{') braceDepth++;
                else if (c === '}') braceDepth--;
                else if (c === '(') parenDepth++;
                else if (c === ')') parenDepth--;
            }

            if (braceDepth < 0 || parenDepth < 0) {
                console.log(`NEGATIVE DEPTH at line ${i+1}: brace=${braceDepth}, paren=${parenDepth}`);
                console.log(`Content: ${line}`);
                break;
            }
        }
        console.log(`Final depths: brace=${braceDepth}, paren=${parenDepth}`);
    }
}
