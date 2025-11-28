// Run: node setup-test-email.js
const https = require('https');

const data = JSON.stringify({});

const options = {
  hostname: 'api.nodemailer.com',
  path: '/user',
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Content-Length': data.length
  }
};

const req = https.request(options, (res) => {
  let body = '';
  res.on('data', (chunk) => body += chunk);
  res.on('end', () => {
    const account = JSON.parse(body);
    console.log('\n=== Ethereal Email Test Account ===');
    console.log('SMTP Host:', account.smtp.host);
    console.log('SMTP Port:', account.smtp.port);
    console.log('Username:', account.user);
    console.log('Password:', account.pass);
    console.log('Web Interface:', account.web);
    console.log('\nAdd to appsettings.json:');
    console.log(`"Email": {
  "SmtpHost": "${account.smtp.host}",
  "SmtpPort": "${account.smtp.port}",
  "FromEmail": "${account.user}",
  "FromPassword": "${account.pass}"
}`);
  });
});

req.write(data);
req.end();
