const fs = require('fs');

const code = fs.readFileSync('scratch/exact_rendered_chat.js', 'utf8');
const lines = code.split('\n');

for (let i = 1540; i < lines.length; i++) {
    console.log(`${i+1}: ${JSON.stringify(lines[i])}`);
}
