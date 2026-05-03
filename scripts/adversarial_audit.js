const fs = require('fs');
const { GoogleGenerativeAI } = require('@google/generative-ai');

async function runAudit(agentName, persona, modelId, genAI, diffContent, geminiMdContent) {
    try {
        console.log(`Starting ${agentName} audit with model: ${modelId}`);
        const model = genAI.getGenerativeModel({ model: modelId });

        const prompt = `You are the ${agentName} (${persona}) performing a Triple-Threat Adversarial Audit.\n`
            + 'Your mandate is to find the "Logical Proof of Failure" (LPF), verify GEMINI.md compliance, and identify load-race condition loopholes.\n\n'
            + 'Institutional Standards (GEMINI.md):\n'
            + geminiMdContent + '\n\n'
            + 'PR DIFF:\n'
            + diffContent + '\n\n'
            + 'TRIPLE-THREAT AUDIT MANDATE:\n'
            + '1. PILLAR I (LPF): Identify the fundamental logical flaw that WILL cause failure. Be adversarial.\n'
            + '2. PILLAR II (COMPLIANCE): Does the code follow GEMINI.md? (Zero-Locks, FSM-Replace, ASCII-Only).\n'
            + '3. PILLAR III (LOAD-RACE): Find the loophole that will cause a crash or race condition under heavy load (e.g., ghost orders, semaphore leaks).\n\n'
            + 'Provide a list of "Forensic Findings" grouped by Pillar and a Final Verdict: PASS or REVISION REQUIRED.\n\n'
            + 'Be the Red Team. If you can break the logic, do it.';

        let result;
        let retries = 3;
        for (let i = 0; i < retries; i++) {
            try {
                result = await model.generateContent(prompt);
                break;
            } catch (err) {
                if (err.message.includes("429") && i < retries - 1) {
                    const delay = Math.pow(2, i) * 5000;
                    console.log(`429 detected for ${agentName}. Retrying in ${delay / 1000}s...`);
                    await new Promise(resolve => setTimeout(resolve, delay));
                    continue;
                }
                throw err;
            }
        }

        const response = await result.response;
        return `### ${agentName} (${persona})\n\n` + response.text();
    } catch (e) {
        console.error(`Error with ${agentName}: ${e.message}`);
        return `### ${agentName} (${persona})\n\n**Audit Failed**: ${e.message}`;
    }
}

async function main() {
    try {
        const apiKey = process.env.GEMINI_API_KEY;
        if (!apiKey) throw new Error("GEMINI_API_KEY missing.");

        const genAI = new GoogleGenerativeAI(apiKey);
        let diffContent = fs.readFileSync('pr.diff', 'utf8');
        let geminiMdContent = '';
        try {
            geminiMdContent = fs.readFileSync('GEMINI.md', 'utf8');
        } catch (e) {}

        const MAX_DIFF_CHARS = 100000;
        if (diffContent.length > MAX_DIFF_CHARS) {
            diffContent = diffContent.substring(0, MAX_DIFF_CHARS) + '\n... [TRUNCATED]';
        }

        const agents = [
            { name: "Jules", persona: "LPF Specialist", model: "gemini-1.5-pro" },
            { name: "Gemini", persona: "Standards Auditor", model: "gemini-1.5-flash" }
        ];

        const results = await Promise.all(agents.map(a => runAudit(a.name, a.persona, a.model, genAI, diffContent, geminiMdContent)));
        
        const finalReport = "## V12 Triple-Threat Adversarial Audit Report\n\n" + results.join("\n\n---\n\n");
        fs.writeFileSync('audit_report.md', finalReport);
    } catch (error) {
        console.error('Fatal Error:', error);
        fs.writeFileSync('audit_report.md', "## V12 Triple-Threat Audit Fatal Error\n\n" + error.message);
    }
}

main();
