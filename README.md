# breakwindow

A lightweight Windows break reminder that waits for you to reach a stopping point.

When a break is due, a small window appears above the tray and stays there until you take the
break. There is no snooze. Walking away for the length of a break counts as a break, and outside
your work hours nothing counts at all.

## Use

Run `breakwindow.exe`. It lives in the tray:

- **Clock in** or **Clock out**: work outside your schedule, or stop early. Clocking in lasts
  until you clock out or until 04:00; clocking out lasts until the next scheduled start.
- **Take break now**: start a break straight away.
- **Open settings**: edit `%APPDATA%\breakwindow\settings.json`. Changes apply as soon as the
  file is saved; an invalid file keeps the last good settings and says so in the tray tooltip.

```json
{
  "schedule": [
    { "days": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"], "start": "08:00", "end": "16:30" }
  ],
  "workMinutes": 55,
  "breakMinutes": 5,
  "idleGraceSeconds": 60,
  "startWithWindows": true
}
```

- `workMinutes`: keyboard and mouse time before a break is due.
- `breakMinutes`: break length; being idle this long also counts as a break.
- `idleGraceSeconds`: a pause shorter than this still counts as work.

## Build

See [AGENTS.md](AGENTS.md) for the build, test and publish commands.
