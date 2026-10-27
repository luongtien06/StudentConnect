const fs = require('fs');

const code = fs.readFileSync('scratch/exact_rendered_chat.js', 'utf8');
const lines = code.split('\n');

// Find where $(document).ready starts
let readyIndex = -1;
for (let i = 0; i < lines.length; i++) {
    if (lines[i].includes("$(document).ready")) {
        readyIndex = i;
        break;
    }
}
console.log("$(document).ready starts at line:", readyIndex + 1);

// Part 1: All code BEFORE $(document).ready
let beforeCode = lines.slice(0, readyIndex).join('\n');
fs.writeFileSync('scratch/part1.js', beforeCode, 'utf8');

// Part 2: Inside $(document).ready (exclude $(document).ready line and last line)
let insideReady = lines.slice(readyIndex + 1, lines.length - 1).join('\n');
fs.writeFileSync('scratch/part2.js', insideReady, 'utf8');
