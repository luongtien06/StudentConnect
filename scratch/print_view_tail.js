const fs = require('fs');

const fullHtml = fs.readFileSync('Views/Chat/Index.cshtml', 'utf8');
const lines = fullHtml.split('\n');

for (let i = lines.length - 10; i < lines.length; i++) {
    console.log(`${i+1}: ${JSON.stringify(lines[i])}`);
}
