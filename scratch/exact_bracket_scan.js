const fs = require('fs');

const code = fs.readFileSync('scratch/exact_rendered_chat.js', 'utf8');

// Lexical scanner to track brace and paren balance with exact line/col
let braceStack = []; // stores { line, col, char }
let parenStack = []; // stores { line, col, char }

let line = 1, col = 0;
let inSingleQuote = false;
let inDoubleQuote = false;
let inTemplate = false;
let inLineComment = false;
let inBlockComment = false;

for (let i = 0; i < code.length; i++) {
    col++;
    let c = code[i];
    let next = i + 1 < code.length ? code[i + 1] : '';

    if (c === '\n') {
        line++;
        col = 0;
        inLineComment = false;
        continue;
    }

    if (inLineComment) continue;

    if (inBlockComment) {
        if (c === '*' && next === '/') {
            inBlockComment = false;
            i++;
            col++;
        }
        continue;
    }

    if (inSingleQuote) {
        if (c === '\\') { i++; col++; }
        else if (c === "'") inSingleQuote = false;
        continue;
    }

    if (inDoubleQuote) {
        if (c === '\\') { i++; col++; }
        else if (c === '"') inDoubleQuote = false;
        continue;
    }

    if (inTemplate) {
        if (c === '\\') { i++; col++; }
        else if (c === '`') inTemplate = false;
        continue;
    }

    // Check comment start
    if (c === '/' && next === '/') {
        inLineComment = true;
        i++;
        col++;
        continue;
    }
    if (c === '/' && next === '*') {
        inBlockComment = true;
        i++;
        col++;
        continue;
    }

    // Check quotes
    if (c === "'") { inSingleQuote = true; continue; }
    if (c === '"') { inDoubleQuote = true; continue; }
    if (c === '`') { inTemplate = true; continue; }

    // Check brackets
    if (c === '{') {
        braceStack.push({ line, col });
    } else if (c === '}') {
        if (braceStack.length === 0) {
            console.log(`EXTRA CLOSING BRACE '}' at line ${line}, col ${col}`);
        } else {
            braceStack.pop();
        }
    } else if (c === '(') {
        parenStack.push({ line, col });
    } else if (c === ')') {
        if (parenStack.length === 0) {
            console.log(`EXTRA CLOSING PAREN ')' at line ${line}, col ${col}`);
        } else {
            parenStack.pop();
        }
    }
}

console.log(`Finished scan:`);
console.log(`Unclosed braces '{': ${braceStack.length}`);
if (braceStack.length > 0) {
    braceStack.forEach(b => console.log(`  Unclosed '{' at line ${b.line}, col ${b.col}`));
}
console.log(`Unclosed parens '(': ${parenStack.length}`);
if (parenStack.length > 0) {
    parenStack.forEach(p => console.log(`  Unclosed '(' at line ${p.line}, col ${p.col}`));
}
