const fs = require('fs');
let html = fs.readFileSync('antigravity_os.html', 'utf-8');

// Fix unused delta
html = html.replace('const delta = clock.getDelta();', '');

// Other potential SonarCloud issues:
// Three.js colors sometimes like hex numbers directly instead of strings or standard CSS strings. The code has:
// colors.purple = new THREE.Color(0xbc13fe);  // This is correct format

fs.writeFileSync('antigravity_os.html', html);
