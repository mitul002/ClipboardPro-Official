const fs = require('fs'); const text = fs.readFileSync('App.xaml', 'utf8'); const matches = [...text.matchAll(/x:Key="([^"]+)"/g)].map(m => m[1]); console.log([...new Set(matches)]);
