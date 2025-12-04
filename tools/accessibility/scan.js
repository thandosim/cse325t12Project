const { chromium } = require('playwright');
const fs = require('fs');
const axeCore = require('axe-core');

(async () => {
  const url = process.argv[2] || 'http://localhost:5004';
  console.log(`Scanning URL: ${url}`);

  const browser = await chromium.launch();
  const page = await browser.newPage();
  try {
    await page.goto(url, { waitUntil: 'networkidle' });
    // inject axe
    await page.addScriptTag({ content: axeCore.source });
    // run axe with wcag2aa ruleset
    const result = await page.evaluate(async () => {
      return await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2aa'] } });
    });

    const outPath = 'axe-report.json';
    fs.writeFileSync(outPath, JSON.stringify(result, null, 2));
    console.log(`Saved report to ${outPath}`);
    console.log(`Violations: ${result.violations.length}`);
    result.violations.forEach(v => {
      console.log(`- ${v.id}: ${v.nodes.length} node(s) — ${v.help}`);
    });
  } catch (err) {
    console.error('Scan failed:', err);
    process.exitCode = 2;
  } finally {
    await browser.close();
  }
})();
