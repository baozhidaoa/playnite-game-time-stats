const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const source = path.join(root, "node_modules", "echarts", "dist", "echarts.min.js");
const target = path.join(root, "web", "js", "vendor", "echarts.min.js");

if (!fs.existsSync(source)) {
  throw new Error("echarts is not installed. Run npm install first.");
}

fs.mkdirSync(path.dirname(target), { recursive: true });
fs.copyFileSync(source, target);
console.log(`Copied ${path.relative(root, source)} -> ${path.relative(root, target)}`);
