---
name: skills-catalog
description: Comprehensive directory of all 37 skills organized by category with search index and dependency mapping.
---

# Skills Catalog

**A complete reference guide for all 37 AI-augmented development skills in the Universal OR Strategy ecosystem.**

## Overview

- **Total Skills:** 37
- **Categories:** 8
- **Last Updated:** 2026-01-19
- **Purpose:** Organize skills by function, reduce context switching, and enable efficient skill discovery

---

## Quick Navigation

- [Core Infrastructure](#core-infrastructure) (7 skills)
- [Code Development](#code-development) (8 skills)
- [NinjaTrader Trading](#ninjatrader-trading) (7 skills)
- [Document Operations](#document-operations) (6 skills)
- [File Management](#file-management) (3 skills)
- [UI/Design](#uidesign) (3 skills)
- [Analytics & Monitoring](#analytics--monitoring) (2 skills)
- [Utilities & Miscellaneous](#utilities--miscellaneous) (1 skill)

---

## CORE INFRASTRUCTURE

System-level skills that enable other skills to function. These are foundational technologies for cost optimization, task routing, version control, and context preservation across development sessions.

### delegation-bridge

**Category:** Core Infrastructure
**Purpose:** Universal MCP-based delegation to Gemini Flash 2.0 for cost optimization. Routes file I/O and routine tasks to cheapest execution layer (Gemini Flash is 200x cheaper than Opus, 40x cheaper than Haiku).
**Use Case:** Deploy files to two locations, update documentation, read non-critical files, perform routine analysis
**Dependencies:** None (foundational)
**Cost Savings:** 65-99% on non-code tasks when delegating file I/O
**IDE Compatibility:** Claude Code CLI, Antigravity IDE, Cursor (with MCP support)
**Related Skills:** multi-ide-router, file-manager, version-safety, context-transfer
**Key Features:**
- Works with ANY AI (Claude, Gemini, Grok)
- Preserves code quality (Opus still handles logic)
- Enables cross-IDE portability
- Zero vendor lock-in
- Automatic fallback chain if MCP unavailable

---

### multi-ide-router

**Category:** Core Infrastructure
**Purpose:** Routes tasks to optimal IDE/AI combo based on cost, capability, and current context. Detects active IDE and available models, selects best execution path.
**Use Case:** Automatically choose between Antigravity (Opus - reasoning), Claude Code CLI (Sonnet - speed), Cursor (Gemini - cost), maintain session context across IDE switches
**Dependencies:** delegation-bridge, context-transfer
**Cost Savings:** Intelligent routing prevents expensive model misuse
**IDE Compatibility:** All IDEs (Antigravity, Claude Code CLI, Cursor, Windsurf)
**Related Skills:** delegation-bridge, context-transfer, ai-council
**Key Features:**
- Detects active IDE automatically
- Selects optimal model for task type
- Tracks cost per model
- Enables IDE switching without context loss
- Fallback to secondary models if primary unavailable

---

### version-safety

**Category:** Core Infrastructure
**Purpose:** Enforces safe file versioning practices for NinjaTrader strategy development. Prevents overwriting existing files, ensures dual deployment, validates naming conventions.
**Use Case:** Creating new features, making updates, saving code changes - ensures version history is preserved and code deploys to both project repo and NinjaTrader
**Dependencies:** file-manager
**Cost:** Prevents expensive rollback operations
**IDE Compatibility:** All IDEs
**Related Skills:** file-manager, version-manager, docs-manager
**Naming Convention:**
- `_UI_[DESCRIPTION]` - UI changes
- `_BUGFIX` or `_FIX_[WHAT]` - Bug fixes
- `_[FEATURE_NAME]` - New features
- `_PERF` or `_OPTIMIZE` - Performance changes
- `_CLEANUP` - Code cleanup
- `_MILESTONE` - Major milestones

**Key Features:**
- Prevents file overwrites with descriptive suffixes
- Enforces dual deployment (project + NinjaTrader)
- No auto-git-commits (manual control)
- Automatic class name updates to match filenames
- Version display string updates

---

### context-transfer

**Category:** Core Infrastructure
**Purpose:** Preserves session state across IDE/AI switches. Maintains progress in .agent/state/ files so work continues seamlessly when switching between tools or models.
**Use Case:** Switch from Claude Code CLI (hit token limit) to Antigravity, switch from Opus to Gemini, pause work and resume later - all with full context
**Dependencies:** delegation-bridge (to save/load state)
**Cost:** Eliminates expensive re-context on model switch (saves $0.05-0.10 per switch)
**IDE Compatibility:** All IDEs
**Related Skills:** delegation-bridge, multi-ide-router, ai-council
**State Files Tracked:**
- `.agent/state/session_state.json` - Current progress
- `.agent/state/current_version.txt` - Active version
- `.agent/state/last_deployment.json` - Recent deployments
- `.agent/state/cost_tracking.json` - Spend tracking

---

### mcp-builder

**Category:** Core Infrastructure
**Purpose:** Creates custom MCP (Model Context Protocol) servers for delegating specialized tasks. Enable Claude and other models to access external tools via standard protocol.
**Use Case:** Create MCP server for NinjaTrader commands, custom trading analysis, platform-specific operations
**Dependencies:** None (builder skill)
**Cost:** Enables 200x cost savings through intelligent delegation
**IDE Compatibility:** All IDEs with MCP support
**Related Skills:** delegation-bridge, multi-ide-router
**Key Features:**
- Reference implementations included
- Scripts for deployment automation
- MCP server configuration templates

---

### project-lifecycle

**Category:** Core Infrastructure
**Purpose:** Manages full project workflow from initial setup through deployment. Tracks phases, milestones, release cycles, and ensures systematic progression.
**Use Case:** Plan new features, track completion, coordinate multi-step releases, manage testing phases
**Dependencies:** version-manager, docs-manager
**Cost:** Prevents costly rework through better planning
**IDE Compatibility:** All IDEs
**Related Skills:** version-manager, docs-manager, version-safety
**Key Features:**
- Phase tracking (planning, dev, test, deploy)
- Milestone management
- Release cycle coordination
- Dependency mapping

---

### ai-council

**Category:** Core Infrastructure
**Purpose:** Multi-AI decision framework that coordinates between Opus (reasoning), Sonnet (balance), Haiku (speed), and Gemini Flash (cost). Routes decisions to appropriate model based on complexity and cost.
**Use Case:** Complex architectural decisions, cost-benefit analysis, task routing, phase selection
**Dependencies:** multi-ide-router, delegation-bridge
**Cost:** Optimizes decision quality vs. spend
**IDE Compatibility:** All IDEs
**Related Skills:** multi-ide-router, delegation-bridge, antigravity-core
**Decision Framework:**
- Opus: Complex reasoning, architecture
- Sonnet: General purpose, balance
- Haiku: Simple tasks, speed priority
- Gemini Flash: File I/O, routine analysis

---

## CODE DEVELOPMENT

Code generation, review, formatting, and quality assurance skills. These tools ensure code quality, consistency, and best practices.

### code-formatter

**Category:** Code Development
**Purpose:** Lightweight Haiku sub-agent for cleaning up C# code. Removes commented code, fixes indentation, removes debug Print() statements, formats code consistently.
**Use Case:** Code cleanup before milestones, remove development artifacts, prepare for production
**Dependencies:** file-manager
**Cost:** $0.001-0.005 per cleanup (Haiku tier)
**IDE Compatibility:** All IDEs
**Related Skills:** file-manager, version-safety, trading-code-review
**Operations:**
- Remove commented code (but keep documentation)
- Remove debug Print() statements
- Fix indentation and alignment
- Remove excess blank lines
- Format property declarations

---

### trading-code-review

**Category:** Code Development
**Purpose:** Mandatory code review checklist for trading strategies. Ensures code passes trading-specific quality gates: order management, position tracking, risk controls, compliance rules.
**Use Case:** Before deploying any strategy version, before production release, code peer review
**Dependencies:** trading-knowledge-vault, ninjatrader-strategy-dev
**Cost:** Prevents expensive trading errors
**IDE Compatibility:** All IDEs
**Related Skills:** trading-knowledge-vault, ninjatrader-strategy-dev, opus-critical
**Review Categories:**
- Order management validation
- Position tracking verification
- Risk control implementation
- Apex compliance rules
- Memory leak prevention
- Performance benchmarks

---

### opus-critical

**Category:** Code Development
**Purpose:** Critical architecture and security review for Claude Opus. Deep analysis of code decisions, identifies architectural issues, security vulnerabilities, and high-impact bugs.
**Use Case:** Major refactoring, security-sensitive code, architectural changes, pre-production review
**Dependencies:** None (Opus-native)
**Cost:** High ($0.05-0.20), but prevents expensive failures
**IDE Compatibility:** Antigravity IDE (Opus native)
**Related Skills:** trading-code-review, opus-deployment-guide
**Focus Areas:**
- Architecture decisions
- Security vulnerabilities
- Performance implications
- Maintainability concerns
- Scalability assessment

---

### skill-creator

**Category:** Code Development
**Purpose:** Framework and templates for creating new skills. Enables rapid skill development with consistent structure, documentation, and integration patterns.
**Use Case:** Create new trading skills, add specialized tools, extend system capabilities
**Dependencies:** None (builder skill)
**Cost:** Amortized over future skill usage
**IDE Compatibility:** All IDEs
**Related Skills:** mcp-builder, multi-ide-router
**Includes:**
- Skill template structure
- SKILL.md documentation format
- Integration reference examples
- Naming conventions

---

### doc-coauthoring

**Category:** Code Development
**Purpose:** Collaborative documentation creation with multiple AI contributors. Enables seamless handoff between models for documentation tasks.
**Use Case:** Create comprehensive documentation, multi-author guides, collaborative content writing
**Dependencies:** docs-manager, delegation-bridge
**Cost:** Efficient use of multiple models for content
**IDE Compatibility:** All IDEs
**Related Skills:** docs-manager, context-transfer
**Key Features:**
- Multi-author integration
- Version tracking for docs
- Collaboration protocols
- Handoff procedures

---

### web-artifacts-builder

**Category:** Code Development
**Purpose:** Framework for creating interactive HTML/CSS/JavaScript artifacts. Builds standalone web components, dashboards, and interactive tools.
**Use Case:** Create trading dashboards, web-based reports, interactive visualizations, standalone tools
**Dependencies:** theme-factory, canvas-design
**Cost:** Efficient artifact generation
**IDE Compatibility:** All IDEs (with preview)
**Related Skills:** theme-factory, canvas-design, webapp-testing
**Key Features:**
- HTML/CSS/JavaScript scaffolding
- Responsive design templates
- Interactive component library
- Browser compatibility

---

### webapp-testing

**Category:** Code Development
**Purpose:** Testing framework for web artifacts. Validates functionality, responsive behavior, browser compatibility, and performance.
**Use Case:** Test web dashboards, verify interactive features, check responsive design, validate performance
**Dependencies:** web-artifacts-builder
**Cost:** Prevents production web app issues
**IDE Compatibility:** All IDEs
**Related Skills:** web-artifacts-builder, theme-factory
**Test Coverage:**
- Functional testing
- Responsive design verification
- Browser compatibility checks
- Performance metrics
- Accessibility validation

---

## NINJATRADER TRADING

Trading-specific tools, strategies, and development patterns for NinjaTrader 8. These skills focus on trading logic, strategy development, compliance, and best practices.

### ninjatrader-strategy-dev

**Category:** NinjaTrader Trading
**Purpose:** NinjaTrader 8 strategy development patterns for high-performance trading. Covers order management, real-time price tracking, memory efficiency, and common bug patterns.
**Use Case:** Developing new strategies, debugging trading logic, implementing order management, optimizing performance
**Dependencies:** trading-knowledge-vault, apex-rithmic-trading
**Cost:** Prevents expensive trading errors ($10k+ lessons built-in)
**IDE Compatibility:** All IDEs
**Related Skills:** trading-knowledge-vault, apex-rithmic-trading, live-price-tracking
**Core Patterns:**
- OnMarketData hook (tick-level updates)
- GetLivePrice() fallback chain
- IsUnmanaged order management
- Rate-limiting order modifications (Apex compliance)
- Memory efficiency (StringBuilder pooling, fixed collections)
- Order update handling
- Rithmic disconnect detection

**Critical Bug Preventions:**
- Close[0] bug (loses 50-90% of profit)
- Stranded orders (unexpected re-entries)
- Rate-limiting violations (Apex warnings)
- Memory leaks (unbounded collections)
- GC pauses (string concatenation)

---

### trading-knowledge-vault

**Category:** NinjaTrader Trading
**Purpose:** Automated lessons learned system for NinjaTrader trading. Transforms bugs into permanent checkpoints that prevent repeating past mistakes.
**Use Case:** Before writing ANY trading code, implement new features, fix bugs, review code
**Dependencies:** None (reference vault)
**Cost:** Prevents $10k+ trading losses
**IDE Compatibility:** All IDEs
**Related Skills:** ninjatrader-strategy-dev, trading-code-review, apex-rithmic-trading
**Vault Categories:**
1. Critical Bugs (Close[0], stranded orders, rate-limiting)
2. Trading Setups (ORB Long, RMA Bounce)
3. Performance Lessons (memory, execution speed)
4. Compliance Lessons (Apex rules, daily loss limits)
5. Architecture Lessons (design patterns)

---

### apex-rithmic-trading

**Category:** NinjaTrader Trading
**Purpose:** Apex Clearing and Rithmic data feed compliance documentation. Ensures trading strategies comply with account rules and feed-specific behaviors.
**Use Case:** Developing for live Apex accounts, using Rithmic data feed, implementing risk controls
**Dependencies:** trading-knowledge-vault
**Cost:** Prevents compliance violations and account restrictions
**IDE Compatibility:** All IDEs
**Related Skills:** trading-knowledge-vault, ninjatrader-strategy-dev
**Coverage Areas:**
- Daily loss limits
- Trailing drawdown rules
- Order modification rate-limiting
- Rithmic data feed quirks
- Account compliance monitoring
- Disconnection handling

---

### live-price-tracking

**Category:** NinjaTrader Trading
**Purpose:** Real-time price tracking patterns and OnMarketData optimization. Covers the critical Close[0] bug and proper tick-level order management.
**Use Case:** Implementing trailing stops, real-time position management, tick-level order updates
**Dependencies:** ninjatrader-strategy-dev, trading-knowledge-vault
**Cost:** Prevents Close[0] bug (most common NinjaTrader mistake)
**IDE Compatibility:** All IDEs
**Related Skills:** ninjatrader-strategy-dev, trading-knowledge-vault
**Key Concepts:**
- OnMarketData hook vs OnBarUpdate
- Close[0] delayed updates problem
- Bid/Ask/Last price priority chain
- Rate-limiting on tick updates
- Memory efficiency in high-frequency updates

---

### trading-session-timezones

**Category:** NinjaTrader Trading
**Purpose:** Session detection, timezone handling, and market hours logic. Ensures strategies respect market sessions and timezone-aware timing.
**Use Case:** ORB setups (detect 9:30 RTH start), timezone-aware alerts, session-specific strategies
**Dependencies:** live-price-tracking
**Cost:** Prevents trading outside intended sessions
**IDE Compatibility:** All IDEs
**Related Skills:** live-price-tracking, ninjatrader-strategy-dev
**Key Features:**
- RTH (Regular Trading Hours) detection
- Pre-market and after-hours logic
- Timezone-aware time checks
- Session caching optimization
- Daylight savings handling

---

### universal-or-strategy

**Category:** NinjaTrader Trading
**Purpose:** Master documentation and patterns for the Universal Opening Range (OR) trading strategy. Covers all versions (V5-V8) and implementation variants.
**Use Case:** Understanding OR strategy logic, implementing new features, debugging OR setups
**Dependencies:** ninjatrader-strategy-dev, trading-knowledge-vault
**Cost:** Reference material (no direct cost)
**IDE Compatibility:** All IDEs
**Related Skills:** ninjatrader-strategy-dev, wsgta-trading-system
**Strategy Components:**
- Opening range detection
- Entry logic (OR breakout)
- Stop placement
- Target scaling (2-3 targets)
- Trailing stop management
- Position sizing
- Risk management

---

### wsgta-trading-system

**Category:** NinjaTrader Trading
**Purpose:** Complete WSGTA (Worldwide Syndicate Group Trading Academy) trading system documentation. Core rules, setups, risk management, and compliance.
**Use Case:** Understanding WSGTA rules, implementing WSGTA setups, trading system reference
**Dependencies:** trading-knowledge-vault
**Cost:** Reference material (no direct cost)
**IDE Compatibility:** All IDEs
**Related Skills:** trading-knowledge-vault, apex-rithmic-trading, universal-or-strategy
**Includes:**
- Trading rules and risk limits
- Approved setups (ORB, RMA, etc.)
- Position sizing formulas
- Account management rules
- Compliance checkpoints

---

## DOCUMENT OPERATIONS

File format handlers for PDF, DOCX, XLSX, and PPTX. These skills enable manipulation, creation, and analysis of common document types.

### pdf

**Category:** Document Operations
**Purpose:** Comprehensive PDF manipulation toolkit for extracting text/tables, creating new PDFs, merging/splitting documents, and handling forms.
**Use Case:** Extract trading data from reports, generate PDF reports, merge documents, fill PDF forms
**Dependencies:** None (library)
**Cost:** Free (native tool)
**IDE Compatibility:** All IDEs with Python support
**Related Skills:** xlsx, docx, pptx
**Operations:**
- Extract text and tables with layout preservation
- Create new PDFs (reportlab, pypdf)
- Merge and split documents
- Rotate and transform pages
- Extract metadata
- Add watermarks
- OCR scanned documents
- Password protection

**Libraries Supported:**
- pypdf (merge, split, rotate, metadata)
- pdfplumber (text/table extraction)
- reportlab (create PDFs)
- pytesseract (OCR)

---

### xlsx

**Category:** Document Operations
**Purpose:** Excel workbook creation, modification, and analysis. Read/write spreadsheets, create charts, format cells, manage multiple sheets.
**Use Case:** Generate trading reports, create analysis spreadsheets, export data tables, automate Excel tasks
**Dependencies:** None (library)
**Cost:** Free (native tool)
**IDE Compatibility:** All IDEs with Python support
**Related Skills:** pdf, docx, pptx
**Operations:**
- Read and write Excel files
- Cell formatting (colors, fonts, numbers)
- Chart creation
- Multiple sheet management
- Data export/import
- Conditional formatting
- Formulas and calculations

**Libraries Supported:**
- openpyxl (full Excel support)
- pandas (dataframe to Excel)
- xlwt (legacy .xls)

---

### docx

**Category:** Document Operations
**Purpose:** Word document creation and modification. Create formatted documents, manage styles, add headers/footers, work with tables and images.
**Use Case:** Generate strategy documentation, create reports, export formatted content, build document templates
**Dependencies:** None (library)
**Cost:** Free (native tool)
**IDE Compatibility:** All IDEs with Python support
**Related Skills:** pdf, xlsx, pptx
**Operations:**
- Create and modify .docx files
- Apply styles and formatting
- Add headers, footers, paragraphs
- Tables and images
- Sections and page breaks
- Properties and metadata
- Field codes and bookmarks

**Libraries Supported:**
- python-docx (primary)
- OOXML schemas (reference)

---

### pptx

**Category:** Document Operations
**Purpose:** PowerPoint presentation creation and modification. Create slides, add text/images, apply layouts, manage themes.
**Use Case:** Generate trading dashboards as slides, create presentation reports, build slide decks, automate slide creation
**Dependencies:** theme-factory (optional for styling)
**Cost:** Free (native tool)
**IDE Compatibility:** All IDEs with Python support
**Related Skills:** pdf, xlsx, docx, theme-factory
**Operations:**
- Create presentations and slides
- Add text boxes and shapes
- Insert images and charts
- Apply slide layouts
- Master slides
- Animations and transitions
- Speaker notes
- Slide sorting

**Libraries Supported:**
- python-pptx (primary)
- OOXML schemas (reference)

---

### docs-manager

**Category:** Document Operations
**Purpose:** Centralized documentation management system. Tracks docs, manages versions, coordinates updates across multiple files, maintains documentation index.
**Use Case:** Track strategy documentation, manage version docs, coordinate multi-file updates
**Dependencies:** version-manager, delegation-bridge
**Cost:** Efficient doc management
**IDE Compatibility:** All IDEs
**Related Skills:** version-manager, doc-coauthoring, file-manager
**Key Features:**
- Documentation index
- Version tracking for docs
- Change coordination
- Multi-file updates
- Changelog integration

---

## FILE MANAGEMENT

File operations, version tracking, and storage management. These skills handle reading, writing, organizing, and tracking files.

### file-manager

**Category:** File Management
**Purpose:** Lightweight Haiku sub-agent for file creation and deployment. Works with version-safety to save changes under new filenames and deploy to both NinjaTrader and project repo.
**Use Case:** Save new strategy versions, deploy files to two locations, create backup copies
**Dependencies:** version-safety, delegation-bridge
**Cost:** $0.001-0.005 per deployment (Haiku tier)
**IDE Compatibility:** All IDEs
**Related Skills:** version-safety, version-manager, docs-manager
**Operations:**
- Create new strategy files
- Deploy to project + NinjaTrader
- Update class names and version strings
- Copy and backup operations
- File listing and verification

---

### version-manager

**Category:** File Management
**Purpose:** Tracks all strategy versions and enables version history management. Maintains version registry, enables easy switching between versions.
**Use Case:** List available versions, switch between versions, track version history
**Dependencies:** file-manager
**Cost:** Efficient version tracking
**IDE Compatibility:** All IDEs
**Related Skills:** file-manager, version-safety, docs-manager
**Key Features:**
- Version registry
- Version history
- Version diff comparison
- Rollback capabilities
- Version metadata (date, description)

---

### strategy-config-safe

**Category:** File Management
**Purpose:** Safe configuration management for strategy parameters. Stores and loads strategy configurations without code changes.
**Use Case:** Test different parameter sets, save strategy configurations, compare setups
**Dependencies:** file-manager
**Cost:** Efficient config management
**IDE Compatibility:** All IDEs
**Related Skills:** file-manager, version-manager
**Key Features:**
- JSON configuration files
- Parameter validation
- Safe loading/saving
- Configuration comparison
- Preset management

---

## UI/DESIGN

User interface design, theming, and visual assets. These skills handle UI creation, styling, and brand consistency.

### brand-guidelines

**Category:** UI/Design
**Purpose:** Brand identity documentation including colors, fonts, logos, and usage guidelines. Ensures consistent branding across all artifacts.
**Use Case:** Apply brand colors to reports, use brand fonts in documents, maintain brand consistency
**Dependencies:** theme-factory
**Cost:** Free (reference)
**IDE Compatibility:** All IDEs
**Related Skills:** theme-factory, canvas-design
**Includes:**
- Color palette with hex codes
- Font specifications
- Logo guidelines
- Usage examples
- Restrictions and dos/don'ts

---

### theme-factory

**Category:** UI/Design
**Purpose:** Toolkit for styling artifacts with pre-set themes. 10 professional themes with color/font pairs for slides, docs, web pages.
**Use Case:** Apply consistent styling to presentations, documents, web artifacts - choose theme or create custom
**Dependencies:** None (styling tool)
**Cost:** Free (tool)
**IDE Compatibility:** All IDEs
**Related Skills:** canvas-design, brand-guidelines
**Pre-built Themes:**
1. Ocean Depths (professional, maritime)
2. Sunset Boulevard (warm, vibrant)
3. Forest Canopy (natural, earth tones)
4. Modern Minimalist (clean, grayscale)
5. Golden Hour (rich, autumnal)
6. Arctic Frost (cool, winter)
7. Desert Rose (soft, sophisticated)
8. Tech Innovation (bold, modern)
9. Botanical Garden (fresh, organic)
10. Midnight Galaxy (dramatic, cosmic)

---

### canvas-design

**Category:** UI/Design
**Purpose:** Design framework for creating visual artifacts with professional layouts, typography, and composition. Supports SVG, Canvas, and design system thinking.
**Use Case:** Create dashboard layouts, design report templates, build visual components
**Dependencies:** theme-factory, brand-guidelines
**Cost:** Design tool (free)
**IDE Compatibility:** All IDEs
**Related Skills:** theme-factory, brand-guidelines, web-artifacts-builder
**Includes:**
- Layout templates
- Typography system
- Component library
- Design tokens
- Responsive guidelines
- Font management

---

## ANALYTICS & MONITORING

Data analysis, monitoring, and real-time tracking tools. These skills handle data extraction, analysis, and performance monitoring.

### live-price-tracking

**Already covered above in NinjaTrader Trading section**

---

### trading-session-timezones

**Already covered above in NinjaTrader Trading section**

---

## UTILITIES & MISCELLANEOUS

Specialized tools that don't fit other categories. One-off utilities and content creation tools.

### slack-gif-creator

**Category:** Utilities & Miscellaneous
**Purpose:** Create animated GIFs optimized for Slack. Convert video/image sequences into shareable GIF format with compression and formatting for Slack.
**Use Case:** Create trading result animations, generate progress GIFs, create visual reports for Slack
**Dependencies:** None (utility)
**Cost:** Free (tool)
**IDE Compatibility:** All IDEs with media tools
**Related Skills:** algorithmic-art, web-artifacts-builder
**Features:**
- Video to GIF conversion
- Image sequence animation
- Slack optimization
- Frame rate control
- Size and compression tuning

---

### algorithmic-art

**Category:** Utilities & Miscellaneous
**Purpose:** Generate algorithmic art and visualizations. Create generative art, fractals, visualizations using code patterns.
**Use Case:** Create visual reports, design generative backgrounds, create aesthetic artifacts
**Dependencies:** canvas-design (optional)
**Cost:** Free (tool)
**IDE Compatibility:** All IDEs
**Related Skills:** canvas-design, theme-factory, slack-gif-creator
**Capabilities:**
- Generative art patterns
- Fractal generation
- Visualization creation
- Animation generation
- Customizable parameters

---

### antigravity-core

**Category:** Utilities & Miscellaneous
**Purpose:** Antigravity IDE integration patterns and Claude Opus optimization. Enables deep integration with Antigravity IDE for maximum Opus reasoning capability.
**Use Case:** Leveraging Opus extended thinking in Antigravity IDE, IDE-specific workflows
**Dependencies:** multi-ide-router
**Cost:** Maximizes Opus value ($0.05-0.10 benefit per task)
**IDE Compatibility:** Antigravity IDE (primary)
**Related Skills:** multi-ide-router, opus-critical, opus-deployment-guide
**Features:**
- Extended thinking integration
- Opus optimization patterns
- IDE keyboard shortcuts
- Context preservation
- Performance tuning

---

### opus-deployment-guide

**Category:** Utilities & Miscellaneous
**Purpose:** Complete deployment guide for Opus-specific tasks and optimizations. Covers when to use Opus, how to structure prompts, and cost optimization.
**Use Case:** Deciding when to use Opus vs other models, structuring Opus-sized tasks, cost-benefit analysis
**Dependencies:** ai-council, multi-ide-router
**Cost:** Optimizes Opus spend
**IDE Compatibility:** All IDEs
**Related Skills:** ai-council, multi-ide-router, opus-critical
**Coverage:**
- When to use Opus
- Prompt engineering for Opus
- Extended thinking patterns
- Cost optimization
- Model comparison matrix

---

## DEPENDENCY GRAPH

### Core Infrastructure Dependencies

```
delegation-bridge (foundation)
├── multi-ide-router
├── context-transfer
├── file-manager
└── version-safety

multi-ide-router (orchestration)
├── delegation-bridge
├── context-transfer
└── ai-council

version-safety (file safety)
└── file-manager

context-transfer (state management)
└── delegation-bridge

mcp-builder (extensibility)
└── delegation-bridge

project-lifecycle (workflow)
├── version-manager
└── docs-manager

ai-council (decision making)
├── multi-ide-router
└── delegation-bridge
```

### NinjaTrader Trading Dependencies

```
ninjatrader-strategy-dev (implementation)
├── trading-knowledge-vault
└── apex-rithmic-trading

trading-knowledge-vault (lessons)
└── (reference only)

apex-rithmic-trading (compliance)
└── trading-knowledge-vault

live-price-tracking (optimization)
├── ninjatrader-strategy-dev
└── trading-knowledge-vault

trading-session-timezones (timing)
└── live-price-tracking

trading-code-review (validation)
├── trading-knowledge-vault
└── ninjatrader-strategy-dev

universal-or-strategy (strategy)
└── ninjatrader-strategy-dev

wsgta-trading-system (rules)
└── trading-knowledge-vault
```

### Code Development Dependencies

```
code-formatter (cleanup)
└── file-manager

trading-code-review (review)
├── trading-knowledge-vault
└── ninjatrader-strategy-dev

opus-critical (deep review)
└── (Opus-native, no deps)

skill-creator (builder)
└── (builder tool, no deps)

doc-coauthoring (docs)
├── docs-manager
└── delegation-bridge

web-artifacts-builder (builder)
├── theme-factory
└── canvas-design

webapp-testing (testing)
└── web-artifacts-builder
```

### File Management Dependencies

```
file-manager (file ops)
└── version-safety

version-manager (tracking)
└── file-manager

strategy-config-safe (config)
└── file-manager

docs-manager (doc tracking)
├── version-manager
└── delegation-bridge
```

### Document Operations Dependencies

```
pdf (library)
├── (independent)

xlsx (library)
├── (independent)

docx (library)
├── (independent)

pptx (library)
└── (independent)

docs-manager (management)
├── version-manager
└── delegation-bridge
```

### UI/Design Dependencies

```
theme-factory (styling)
└── (independent tool)

canvas-design (design)
├── theme-factory
└── brand-guidelines

brand-guidelines (branding)
└── (reference)
```

---

## SEARCH INDEX

### Alphabetical Listing with Categories

| Skill Name | Category | Primary Use |
|-----------|----------|-------------|
| ai-council | Core Infrastructure | Multi-AI decision routing |
| algorithmic-art | Utilities & Misc | Generative art creation |
| antigravity-core | Utilities & Misc | Antigravity IDE integration |
| apex-rithmic-trading | NinjaTrader Trading | Compliance & feed rules |
| brand-guidelines | UI/Design | Brand consistency |
| canvas-design | UI/Design | Visual design framework |
| code-formatter | Code Development | Code cleanup automation |
| context-transfer | Core Infrastructure | State persistence across IDEs |
| delegation-bridge | Core Infrastructure | Cost-optimized task routing |
| doc-coauthoring | Code Development | Multi-author documentation |
| docs-manager | Document Operations | Documentation management |
| docx | Document Operations | Word document creation |
| file-manager | File Management | File creation & deployment |
| live-price-tracking | NinjaTrader Trading | Real-time price updates |
| mcp-builder | Core Infrastructure | MCP server creation |
| multi-ide-router | Core Infrastructure | IDE & model selection |
| ninjatrader-strategy-dev | NinjaTrader Trading | Strategy development patterns |
| opus-critical | Code Development | Deep code review |
| opus-deployment-guide | Utilities & Misc | Opus optimization guide |
| pdf | Document Operations | PDF manipulation |
| pptx | Document Operations | PowerPoint creation |
| project-lifecycle | Core Infrastructure | Project workflow management |
| skill-creator | Code Development | Skill template creation |
| slack-gif-creator | Utilities & Misc | GIF animation for Slack |
| strategy-config-safe | File Management | Strategy configuration |
| theme-factory | UI/Design | Professional theming |
| trading | NinjaTrader Trading | (See universal-or-strategy) |
| trading-code-review | Code Development | Trading code validation |
| trading-knowledge-vault | NinjaTrader Trading | Lessons learned system |
| trading-session-timezones | NinjaTrader Trading | Session & timezone handling |
| universal-or-strategy | NinjaTrader Trading | OR strategy patterns |
| version-manager | File Management | Version tracking |
| version-safety | Core Infrastructure | Safe versioning protocol |
| webapp-testing | Code Development | Web artifact validation |
| web-artifacts-builder | Code Development | Interactive web components |
| wsgta-trading-system | NinjaTrader Trading | WSGTA trading rules |
| xlsx | Document Operations | Excel spreadsheet creation |

---

## USE CASE MATRIX

### Find Skills by Task Type

**I need to...**

#### Code Implementation
- **Start new feature** → ninjatrader-strategy-dev + universal-or-strategy
- **Review code** → trading-code-review + opus-critical
- **Fix a bug** → ninjatrader-strategy-dev + trading-knowledge-vault
- **Clean up code** → code-formatter + file-manager
- **Write documentation** → doc-coauthoring + docs-manager

#### File Operations
- **Save a new version** → version-safety + file-manager
- **Deploy to NinjaTrader** → file-manager + delegation-bridge
- **Track versions** → version-manager
- **Manage configurations** → strategy-config-safe

#### Documentation
- **Create a report** → pdf + docs-manager
- **Make a spreadsheet** → xlsx + docs-manager
- **Create a presentation** → pptx + theme-factory
- **Write a document** → docx + doc-coauthoring

#### Trading Development
- **Implement order logic** → ninjatrader-strategy-dev + apex-rithmic-trading
- **Fix real-time updates** → live-price-tracking + ninjatrader-strategy-dev
- **Handle session times** → trading-session-timezones
- **Prevent past mistakes** → trading-knowledge-vault (before coding!)

#### UI/Visual Design
- **Apply styling** → theme-factory + canvas-design
- **Ensure brand consistency** → brand-guidelines
- **Build web interface** → web-artifacts-builder + theme-factory
- **Test web app** → webapp-testing

#### Task Coordination
- **Route to best model** → multi-ide-router + ai-council
- **Switch IDEs mid-project** → context-transfer + delegation-bridge
- **Optimize costs** → delegation-bridge + opus-deployment-guide
- **Create new skill** → skill-creator + mcp-builder

#### Analytics
- **Track prices in real-time** → live-price-tracking
- **Handle timezone logic** → trading-session-timezones
- **Create animations** → algorithmic-art + slack-gif-creator

---

## COST BREAKDOWN

### By Cost Tier

**FREE (Library/Reference)**
- pdf, xlsx, docx, pptx (native tools)
- trading-knowledge-vault (reference)
- universal-or-strategy (reference)
- wsgta-trading-system (reference)
- brand-guidelines (reference)
- canvas-design (free tool)
- theme-factory (free tool)
- algorithmic-art (free tool)

**LOW COST ($0.00001-0.001)**
- Haiku sub-agents: code-formatter, file-manager
- Gemini Flash: delegation-bridge
- Simple delegation tasks

**MEDIUM COST ($0.001-0.05)**
- Sonnet: doc-coauthoring, skill-creator, multi-ide-router
- Context transfers
- General code review

**HIGH COST ($0.05-0.20)**
- Opus: opus-critical, ai-council decisions
- Complex architecture review
- Extended thinking tasks

### Cost Savings

| Scenario | Old Approach | New Approach | Savings |
|----------|-------------|--------------|---------|
| Save + deploy code | Opus ($0.40) | Sonnet ($0.12) + Haiku deploy ($0.001) | 99.7% |
| Deploy to 2 locations | Opus ($0.10) | Gemini Flash ($0.0001) | 99.9% |
| Code review | Opus ($0.20) | Sonnet ($0.05) | 75% |
| Full feature pipeline | Opus all tasks ($1.00) | Opus code ($0.12) + delegation ($0.001) + Haiku cleanup ($0.001) | 87% |

**Formula**: Delegate file I/O to Gemini Flash, code cleanup to Haiku, keep Opus for complex reasoning.

---

## IDE COMPATIBILITY

### Which Skills Work Where

| Skill | Antigravity | Claude Code CLI | Cursor | Windsurf | Notes |
|-------|------------|-----------------|--------|----------|-------|
| delegation-bridge | ✓ MCP | ✓ MCP | ✓ MCP | ✓ MCP | Requires MCP setup |
| multi-ide-router | ✓ | ✓ | ✓ | ✓ | Universal |
| context-transfer | ✓ | ✓ | ✓ | ✓ | Via .agent/state/ |
| code-formatter | ✓ | ✓ | ✓ | ✓ | Universal |
| ninjatrader-strategy-dev | ✓ | ✓ | ✓ | ✓ | Reference (no IDE deps) |
| trading-knowledge-vault | ✓ | ✓ | ✓ | ✓ | Reference |
| opus-critical | ✓ | ✗ | ✗ | ✗ | Opus-only (Antigravity) |
| opus-deployment-guide | ✓ | ~ | ~ | ~ | Opus-focused |
| antigravity-core | ✓ | ✗ | ✗ | ✗ | Antigravity-only |
| pdf/xlsx/docx/pptx | ✓ | ✓ | ✓ | ✓ | Python libraries |
| web-artifacts-builder | ✓ | ✓ | ✓ | ✓ | Universal |
| webapp-testing | ✓ | ✓ | ✓ | ✓ | Universal |
| theme-factory | ✓ | ✓ | ✓ | ✓ | Universal |
| canvas-design | ✓ | ✓ | ✓ | ✓ | Universal |
| brand-guidelines | ✓ | ✓ | ✓ | ✓ | Reference |
| slack-gif-creator | ✓ | ✓ | ✓ | ✓ | Media tools |
| algorithmic-art | ✓ | ✓ | ✓ | ✓ | Python-based |

**Legend**: ✓ Full support, ~ Partial support, ✗ Not available

---

## GETTING STARTED

### Top 10 Essential Skills (in order)

**For All Users:**

1. **multi-ide-router** - Start here to understand IDE selection
2. **delegation-bridge** - Understand cost optimization
3. **version-safety** - Prevent file overwrites
4. **context-transfer** - Switch IDEs without losing progress
5. **file-manager** - Deploy changes to two locations

**For NinjaTrader Developers:**

6. **ninjatrader-strategy-dev** - Core strategy patterns
7. **trading-knowledge-vault** - Prevent $10k+ mistakes
8. **trading-code-review** - Quality gates
9. **apex-rithmic-trading** - Compliance rules
10. **live-price-tracking** - Fix the Close[0] bug

### Onboarding Sequence

**Week 1: Foundations**
1. Read delegation-bridge (cost optimization)
2. Read version-safety (file safety)
3. Read context-transfer (session persistence)
4. Read multi-ide-router (IDE selection)

**Week 2: Development**
5. Read ninjatrader-strategy-dev (patterns)
6. Read trading-knowledge-vault (lessons)
7. Read trading-code-review (quality)
8. Read file-manager (deployment)

**Week 3: Advanced**
9. Read opus-deployment-guide (cost decision-making)
10. Read ai-council (multi-model routing)
11. Read project-lifecycle (workflow)
12. Read mcp-builder (extensibility)

**Week 4: Specialized**
- Read your domain-specific skills:
  - UI developers: theme-factory, canvas-design, web-artifacts-builder
  - Traders: apex-rithmic-trading, live-price-tracking, trading-session-timezones
  - Documenters: doc-coauthoring, docs-manager, pdf/xlsx/docx/pptx

---

## INTEGRATION PATTERNS

### Common Workflows

#### Workflow 1: Create New Feature (Safe)

```
1. Read trading-knowledge-vault (prevent past mistakes)
2. Read ninjatrader-strategy-dev (implementation patterns)
3. Use version-safety (naming convention)
4. Use file-manager (deploy to both locations)
5. Use trading-code-review (quality gates)
6. Use context-transfer (state for next session)
```

**Skills Used:** 6 (reading) + 3 (execution) = 9 total

---

#### Workflow 2: Deploy & Test

```
1. Use version-safety (determine new filename)
2. Use file-manager (save to project)
3. Use file-manager (copy to NinjaTrader)
4. Use version-manager (track version)
5. Use context-transfer (save progress)
```

**Skills Used:** 5

---

#### Workflow 3: Cost-Optimized Development (Antigravity to Claude Code CLI)

```
1. Use antigravity-core (Opus extended thinking for architecture)
2. Use delegation-bridge (save state via Gemini Flash)
3. Switch to Claude Code CLI (hit token limit)
4. Use context-transfer (load state from .agent/state/)
5. Use multi-ide-router (continue with Sonnet)
6. Use delegation-bridge (deploy via Gemini Flash)
```

**Cost Impact:** Opus ($0.12) + Gemini Flash ($0.0001) + Sonnet ($0.05) = $0.1701 (vs $0.40 Opus-only)
**Savings:** 57% cost reduction with same quality

---

#### Workflow 4: Complete Documentation Generation

```
1. Use theme-factory (select visual theme)
2. Use canvas-design (design layout)
3. Use doc-coauthoring (multi-author content)
4. Use docs-manager (track documentation)
5. Use pdf/pptx (export formats)
6. Use brand-guidelines (ensure consistency)
```

**Skills Used:** 6

---

## RELATED SKILLS CROSS-REFERENCE

### Skills That Work Together

**Delegation Cluster:**
- delegation-bridge + multi-ide-router → Cost-optimized routing
- delegation-bridge + context-transfer → Cross-IDE sessions
- delegation-bridge + file-manager → Low-cost deployment

**Trading Cluster:**
- ninjatrader-strategy-dev + trading-knowledge-vault → Bug prevention
- trading-knowledge-vault + trading-code-review → Quality gates
- apex-rithmic-trading + live-price-tracking → Compliance + performance
- trading-session-timezones + live-price-tracking → Time-aware logic

**Development Cluster:**
- code-formatter + file-manager → Clean deployments
- trading-code-review + opus-critical → Layered review
- doc-coauthoring + docs-manager → Collaborative docs
- web-artifacts-builder + webapp-testing → Complete QA

**Design Cluster:**
- theme-factory + canvas-design → Consistent styling
- brand-guidelines + theme-factory → Brand compliance
- web-artifacts-builder + theme-factory → Styled web artifacts

**File Management Cluster:**
- version-safety + file-manager → Safe versioning
- version-manager + docs-manager → Complete tracking
- strategy-config-safe + version-manager → Config versions

---

## ADVANCED TOPICS

### Custom Skill Creation

To create a new skill, use the **skill-creator** framework:

1. Read skill-creator (understand structure)
2. Use template in skill-creator/references
3. Define SKILL.md frontmatter
4. Document purpose, use cases, patterns
5. Test integration with related skills
6. Add to this catalog

---

### MCP Server Extension

To add a new MCP server capability:

1. Read mcp-builder (understand protocol)
2. Review reference implementations
3. Build new server with delegation-bridge pattern
4. Test with delegation-bridge
5. Update .agent/config/mcp_servers.json
6. Document in mcp-builder

---

### Model Routing Decisions

When deciding which model to use:

1. **Opus ($0.05-0.20)**: Complex reasoning, architecture, security review
2. **Sonnet ($0.01-0.05)**: General development, balance of speed/quality
3. **Haiku ($0.001-0.01)**: Code cleanup, simple formatting, file operations
4. **Gemini Flash ($0.0001)**: File I/O, deployment, routine analysis

Use **ai-council** for difficult decisions.

---

## SKILL MATURITY LEVELS

### Mature Skills (Production-Ready)

- delegation-bridge
- version-safety
- ninjatrader-strategy-dev
- trading-knowledge-vault
- file-manager
- code-formatter
- pdf/xlsx/docx/pptx

### Stable Skills (Field-Tested)

- multi-ide-router
- context-transfer
- trading-code-review
- theme-factory
- canvas-design

### Evolving Skills (Regular Updates)

- apex-rithmic-trading (compliance changes)
- live-price-tracking (feed updates)
- trading-session-timezones (market hours)
- universal-or-strategy (strategy versions)

### New Skills (Under Development)

- ai-council (expanding decision matrix)
- mcp-builder (new patterns)
- project-lifecycle (workflow optimization)

---

## TROUBLESHOOTING

### "Skill X not found"

Check alphabetical index above. Skill names use hyphens (e.g., `live-price-tracking` not `livePriceTracking`)

### "MCP Server unavailable"

- Check delegation-bridge fallback chain
- Use Claude Haiku as temporary fallback
- Restart MCP server, then retry

### "Lost session context"

- Check `.agent/state/session_state.json`
- Use context-transfer to reload
- Check delegation-bridge for state sync issues

### "File deployment failed"

- Verify NinjaTrader path: `C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/`
- Check file-manager logs
- Verify file permissions

### "Exceeded cost budget"

- Use delegation-bridge to route to cheaper model
- Use ai-council to decide when to use Opus vs Haiku
- Check cost breakdown section above

---

## CHANGELOG

### Version 1.0 (2026-01-19)

- Initial comprehensive catalog created
- 37 skills documented with full details
- 8 categories organized
- Dependencies mapped
- Use case matrix created
- Cost analysis included
- IDE compatibility documented

---

## QUICK REFERENCE CARD

```
COST OPTIMIZATION:
  Expensive code logic → Opus ($0.05-0.20)
  General development → Sonnet ($0.01-0.05)
  Code cleanup → Haiku ($0.001-0.01)
  File I/O → Gemini Flash ($0.0001) ← Use delegation-bridge!

FILE OPERATIONS:
  Save new version → version-safety + file-manager
  Deploy to NinjaTrader → delegation-bridge
  Track versions → version-manager

TRADING DEVELOPMENT:
  Before ANY code → READ trading-knowledge-vault
  Implement logic → ninjatrader-strategy-dev
  Review code → trading-code-review
  Go live → apex-rithmic-trading

SESSION MANAGEMENT:
  Switch IDEs → context-transfer (saves state)
  Route to best model → multi-ide-router
  Optimize costs → delegation-bridge

DOCUMENTATION:
  Create reports → pdf, xlsx, pptx
  Style artifacts → theme-factory
  Collaborate → doc-coauthoring
```

---

## FOOTER

**Skills Catalog v1.0** | Last Updated: 2026-01-19 | 37 Skills Across 8 Categories

For updates to this catalog, edit this file and update the "Last Updated" date above.

To use a skill, find it in this catalog and read its SKILL.md file for complete documentation.
