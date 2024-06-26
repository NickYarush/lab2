const http = require('http');
const fs = require('fs');

const server = http.createServer((req, res) => {
    // Разрешаем доступ для всех источников (можно более строго настроить)
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

    if (req.method === 'POST' && req.url === '/submit-data') {
        let body = '';
        req.on('data', chunk => {
            body += chunk.toString(); // Получаем данные POST запроса
            console.log('Received data chunk:', chunk.toString()); // Добавляем отладочную информацию
        });
        req.on('end', () => {
            // Записываем полученные данные в файл log.txt
            fs.appendFile('log.txt', body + '\n', err => {
                if (err) {
                    console.error('Error writing to log file:', err);
                    res.statusCode = 500;
                    res.end('Error writing to log file');
                } else {
                    console.log('Data written to log file:', body);
                    res.statusCode = 200;
                    res.end('Data received and logged');
                }
            });
        });
    } else if (req.method === 'OPTIONS') {
        // Для предварительного запроса (preflight request) от браузера
        res.writeHead(200);
        res.end();
    } else {
        res.statusCode = 404;
        res.end('Not Found');
    }
});

const port = 3000;
server.listen(port, () => {
    console.log(`Server running at http://localhost:${port}`);
});