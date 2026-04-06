# ADSR Dashboard – WinForms Prototype (v6)

All v5 review points addressed. Targets 1920x1080 maximised.

## Prerequisites
* **Operating System**: Windows (Required for Windows Forms)
* **SDK**: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer

## Getting Started

### Option 1: Clone the Repository
If you have Git installed, run the following commands:

```bash
git clone https://github.com/YOUR_USERNAME/ADSRDashboard.git
cd ADSRDashboard
dotnet run
```

## What changed in v6

| # | Item | Change |
|---|------|--------|
| 1 | Gear animation removed | Machine State gear image is now static |
| 2 | Logo replaced | Header uses dashboard_m_l.png analytics icon |
| 3 | Machine bar width | Now inside left column, matches bin panel width |
| 4 | Alert Center full height | Stretches full body height, not just bin height |
| 5 | Bin area Z-order fixed | Background images sit behind buttons/controls |
| 6 | Fan popup - On/Off toggle | Pill switch replaces trackbar slider |
| 7 | bee_stop overlay | Stop button shows full-screen overlay with Resume button |
| 8 | Footer version text LEFT | ASRS GUI label left-aligned next to GHT logo |
