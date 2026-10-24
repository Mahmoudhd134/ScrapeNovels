// Run headless Claude Code agents (`claude -p`), keeping a fixed number running at once.
import fs from "node:fs";
import { spawn } from "node:child_process";

// Without ANTHROPIC_API_KEY the CLI uses the signed-in Claude account, not API billing.
export function runAgent({ prompt, cwd, addDirs, model = "opus", effort = "medium", logFile }) {
  const args = [
    "-p", prompt,
    "--model", model,
    "--effort", effort,
    "--output-format", "json",
    "--no-session-persistence",
    "--add-dir", ...addDirs,
    "--allowedTools", "Read", "Write", "Bash(node:*)", "Bash(python:*)",
  ];
  const env = { ...process.env };
  delete env.ANTHROPIC_API_KEY;
  return new Promise((resolve) => {
    const started = Date.now();
    const child = spawn("claude", args, { cwd, env, windowsHide: true });
    let stdout = "";
    let stderr = "";
    child.stdout.on("data", (d) => (stdout += d));
    child.stderr.on("data", (d) => (stderr += d));
    child.on("close", (code) => {
      const seconds = Math.round((Date.now() - started) / 1000);
      if (logFile) fs.writeFileSync(logFile, JSON.stringify({ code, seconds, stdout, stderr }, null, 1));
      resolve({ code, seconds });
    });
  });
}

// Process `items` with exactly `concurrency` workers (fewer only when fewer items remain).
export async function pool(items, concurrency, work) {
  const queue = [...items];
  await Promise.all(
    Array.from({ length: Math.min(concurrency, queue.length) }, async () => {
      while (queue.length) await work(queue.shift());
    }),
  );
}
