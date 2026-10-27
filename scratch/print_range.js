const fs = require('fs');

const code = fs.readFileSync('scratch/part2.js', 'utf8');
const lines = code.split('\n');

for (let i = 465; i <= 535 && i < lines.length; i++) {
    console.log(`${i+1}: ${lines[i]}`);
}
