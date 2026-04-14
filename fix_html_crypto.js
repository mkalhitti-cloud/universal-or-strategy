const fs = require('fs');
let html = fs.readFileSync('antigravity_os.html', 'utf-8');

// Replace all Math.random() with window.crypto.getRandomValues for SonarCloud security hotspots
html = html.replace(/Math\.random\(\)/g, '(window.crypto.getRandomValues(new Uint32Array(1))[0] / 4294967295)');
html = html.replace(/THREE\.MathUtils\.randFloatSpread\((.*?)\)/g, '((window.crypto.getRandomValues(new Uint32Array(1))[0] / 4294967295) - 0.5) * $1');

fs.writeFileSync('antigravity_os.html', html);
