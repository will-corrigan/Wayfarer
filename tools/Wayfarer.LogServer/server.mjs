// A tiny receiver for Wayfarer's remote log: POST /log with a JSON array of entries
// { time, level, message } and it appends them to logs/<date>.jsonl and prints them.
// Developer-only, LAN-only, no dependencies. Run: node server.mjs [port] [logDir]
import { createServer } from "node:http";
import { appendFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";

const port = Number(process.argv[2] ?? 7790);
const logDir = process.argv[3] ?? join(process.cwd(), "logs");
mkdirSync(logDir, { recursive: true });

const server = createServer((request, response) => {
  if (request.method !== "POST" || request.url !== "/log") {
    response.writeHead(404).end();
    return;
  }
  let body = "";
  request.on("data", (chunk) => (body += chunk));
  request.on("end", () => {
    try {
      const entries = JSON.parse(body);
      const file = join(logDir, `${new Date().toISOString().slice(0, 10)}.jsonl`);
      const from = request.socket.remoteAddress;
      for (const entry of entries) {
        appendFileSync(file, JSON.stringify({ from, ...entry }) + "\n");
        console.log(`${entry.time} [${entry.level}] ${entry.message}`);
      }
      response.writeHead(204).end();
    } catch (error) {
      console.error("bad batch:", error.message);
      response.writeHead(400).end();
    }
  });
});

server.listen(port, "0.0.0.0", () => console.log(`Wayfarer log server on port ${port}, writing to ${logDir}`));
