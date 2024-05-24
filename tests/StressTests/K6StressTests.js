import http from 'k6/http';
import { sleep } from 'k6';

export const options = {
    vus: 2,
    duration: '30s',
};

const longDataToLoadUrl = 'http://127.0.0.1:8080/main.js';
const otherServerLongDataToLoadUrl = 'https://localhost:7110/js/main.js';
export default function () {
    http.get(longDataToLoadUrl);
    //http.get(otherServerLongDataToLoadUrl);
    sleep(1);
}