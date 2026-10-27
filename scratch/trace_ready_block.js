const fs = require('fs');

const code = fs.readFileSync('scratch/exact_rendered_chat.js', 'utf8');
const lines = code.split('\n');

// Scan within $(document).ready block (from line 883)
let stack = [];
for (let i = 882; i < lines.length; i++) {
    let l = lines[i];
    let cleaned = l
        .replace(/'(\\.|[^'])*'/g, "''")
        .replace(/"(\\.|[^"])*"/g, '""')
        .replace(/`(\\.|[^`])*`/g, '``')
        .replace(/\/\/.*$/, '');

    for (let j = 0; j < cleaned.length; j++) {
        let c = cleaned[j];
        if (c === '{') {
            stack.push({ line: i + 1, text: l.trim().substring(0, 40) });
        } else if (c === '}') {
            if (stack.length > 0) {
                let open = stack.pop();
                // console.log(`Closed { from line ${open.line} at line ${i+1}`);
            } else {
                console.log(`EXTRA '}' at line ${i+1}: ${l}`);
            }
        }
    }
}

console.log("Remaining unclosed braces inside ready block:");
stack.forEach(s => console.log(`  Line ${s.line}: ${s.text}`));
