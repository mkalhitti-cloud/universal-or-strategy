// Quick check of the generated HTML
const fs = require('fs');
let html = fs.readFileSync('antigravity_os.html', 'utf-8');
console.log(html.match(/Math\.random\(\)/g));
console.log(html.match(/THREE\.MathUtils\.randFloatSpread/g));
