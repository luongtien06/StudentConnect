const fs = require('fs');
const vm = require('vm');

const html = fs.readFileSync('Views/Chat/Index.cshtml', 'utf8');

const scriptRegex = /<script\b[^>]*>([\s\S]*?)<\/script>/gi;
let match;
let scriptIndex = 0;

while ((match = scriptRegex.exec(html)) !== null) {
    scriptIndex++;
    let jsCode = match[1];

    let lines = jsCode.split('\n');
    let sanitizedLines = lines.map((line, idx) => {
        let l = line;
        l = l.replace(/'@[^']*'/g, "'1'");
        l = l.replace(/"@[^"]*"/g, '"1"');
        if (l.trim().startsWith('@if')) {
            return 'if (true) {';
        }
        l = l.replace(/<text>|<\/text>/g, '');
        l = l.replace(/@\{/g, '{');
        l = l.replace(/@\(.*?\);/g, '0;');
        l = l.replace(/@\w+(\(.*?\))?/g, '0');
        return l;
    });

    let sanitized = sanitizedLines.join('\n');

    try {
        new vm.Script(sanitized);
        console.log(`[PASS] Script block #${scriptIndex}: Syntax is 100% VALID`);
    } catch (err) {
        console.error(`[FAIL] Script block #${scriptIndex}: Syntax Error!`);
        console.error(err.message);
        console.error('Stack:', err.stack);
    }
}
