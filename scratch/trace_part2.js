const fs = require('fs');

const code = fs.readFileSync('scratch/part2.js', 'utf8');
const lines = code.split('\n');

// Binary search to find where unclosed bracket starts
// We wrap slices in "function test() { <slice> }" and check where it breaks
for (let i = 1; i <= lines.length; i++) {
    let chunk = lines.slice(0, i).join('\n');
    let testCode = "function __test__() {\n" + chunk + "\n}";
    // Let's count unclosed { in chunk
    let openCount = 0;
    let inStr = false;
    let quote = '';
    for (let c of chunk) {
        if (!inStr && (c === '"' || c === "'" || c === '`')) {
            inStr = true;
            quote = c;
        } else if (inStr && c === quote) {
            inStr = false;
        } else if (!inStr) {
            if (c === '{') openCount++;
            else if (c === '}') openCount--;
        }
    }
    if (i % 50 === 0 || i === lines.length) {
        console.log(`Line ${i}: open braces = ${openCount}`);
    }
}
