import { useState } from 'react';
import Section1_LambdaSites from './components/Section1_LambdaSites';
import Section2_PathConsistency from './components/Section2_PathConsistency';
import Section3_VerificationSteps from './components/Section3_VerificationSteps';
import Section4_Summary from './components/Section4_Summary';

const MODEL_NAME = 'Claude Opus 4.5';
const BUILD_TAG_OLD = 'Build 1111.002-v28.0';
const BUILD_TAG_NEW = 'Build 1111.003-v28.0-adr019';

const navItems = [
  { id: 'section1', label: 'Lambda Sites', short: 'C.1', color: 'bg-blue-700' },
  { id: 'section2', label: 'Path Consistency', short: 'D.4', color: 'bg-purple-700' },
  { id: 'section3', label: 'Verification Steps', short: 'F', color: 'bg-teal-700' },
  { id: 'section4', label: 'Overall Summary', short: 'Σ', color: 'bg-rose-700' },
];

export default function App() {
  const [activeNav, setActiveNav] = useState('section1');

  const scrollTo = (id: string) => {
    setActiveNav(id);
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 font-sans">
      {/* Header */}
      <header className="sticky top-0 z-50 bg-slate-950/95 backdrop-blur border-b border-slate-800">
        <div className="max-w-7xl mx-auto px-4 py-3">
          <div className="flex flex-col md:flex-row md:items-center gap-3 md:gap-6">
            {/* Brand */}
            <div className="flex-1 min-w-0">
              <h2 className="text-lg font-black tracking-tight text-white leading-tight">
                <span className="text-blue-400">{MODEL_NAME}</span>
                <span className="text-slate-400 font-normal text-sm ml-2">· Senior Architect Review</span>
              </h2>
              <div className="flex items-center gap-2 mt-0.5 flex-wrap">
                <span className="text-xs text-slate-500">ADR-019 Sovereign Substrate Repair</span>
                <span className="text-slate-600">·</span>
                <span className="text-xs font-mono text-red-400">{BUILD_TAG_OLD}</span>
                <span className="text-slate-500 text-xs">→</span>
                <span className="text-xs font-mono text-green-400">{BUILD_TAG_NEW}</span>
              </div>
            </div>

            {/* Verdict pill */}
            <div className="flex items-center gap-2 shrink-0">
              <span className="bg-rose-900/70 border border-rose-600 text-rose-300 text-xs font-bold px-3 py-1.5 rounded-full">
                🚫 NEEDS PLAN UPDATE
              </span>
              <span className="bg-slate-800 border border-slate-600 text-slate-300 text-xs px-2 py-1.5 rounded-full">
                3 blocking · 3 moderate · 4 advisory
              </span>
            </div>
          </div>

          {/* Nav */}
          <nav className="flex gap-1 mt-3 overflow-x-auto pb-0.5">
            {navItems.map(item => (
              <button
                key={item.id}
                onClick={() => scrollTo(item.id)}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded text-xs font-semibold whitespace-nowrap transition-colors ${
                  activeNav === item.id
                    ? 'bg-slate-700 text-white'
                    : 'text-slate-400 hover:text-white hover:bg-slate-800'
                }`}
              >
                <span className={`${item.color} text-white text-xs px-1.5 py-0.5 rounded font-bold`}>{item.short}</span>
                {item.label}
              </button>
            ))}
          </nav>
        </div>
      </header>

      {/* Intro banner */}
      <div className="bg-gradient-to-r from-slate-900 via-blue-950/30 to-slate-900 border-b border-slate-800">
        <div className="max-w-7xl mx-auto px-4 py-5">
          <div className="flex flex-wrap gap-4 items-start">
            <div className="flex-1 min-w-64">
              <div className="text-xs text-blue-400 font-semibold uppercase tracking-wide mb-1">Document Accessed</div>
              <code className="text-xs text-slate-300 break-all">
                github.com/mkalhitti-cloud/universal-or-strategy @ mission-uni-5-full-sync · docs/brain/implementation_plan.md
              </code>
            </div>
            <div className="flex-1 min-w-64">
              <div className="text-xs text-green-400 font-semibold uppercase tracking-wide mb-1">Build Tag Delta (exact, from §Header)</div>
              <div className="font-mono text-sm">
                <span className="text-red-400">{BUILD_TAG_OLD}</span>
                <span className="text-slate-500"> → </span>
                <span className="text-green-400">{BUILD_TAG_NEW}</span>
              </div>
              <div className="text-xs text-slate-500 mt-0.5">src/V12_002.Constants.cs:12</div>
            </div>
            <div className="flex-1 min-w-48">
              <div className="text-xs text-amber-400 font-semibold uppercase tracking-wide mb-1">Consistency Gate</div>
              <div className="text-xs text-slate-300">Status: <span className="text-amber-300 font-bold">ARENA ADJUDICATION PENDING</span></div>
              <div className="text-xs text-slate-400">P4 handoff SUSPENDED per Director 2026-04-18</div>
            </div>
          </div>
        </div>
      </div>

      {/* Main content */}
      <main className="max-w-7xl mx-auto px-4 py-8">
        <Section1_LambdaSites />
        <Section2_PathConsistency />
        <Section3_VerificationSteps />
        <Section4_Summary />
      </main>

      {/* Footer */}
      <footer className="border-t border-slate-800 bg-slate-950">
        <div className="max-w-7xl mx-auto px-4 py-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="text-xs text-slate-500">
              <span className="text-blue-400 font-semibold">{MODEL_NAME}</span> · Technical Design Review Dashboard · ADR-019 Sovereign Substrate Repair
            </div>
            <div className="text-xs text-slate-600">
              All findings cite document sections · Based strictly on what was read · No external assumptions
            </div>
          </div>
        </div>
      </footer>
    </div>
  );
}
