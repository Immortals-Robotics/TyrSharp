# Tyr: God of valor and justice
[![.NET](https://github.com/Immortals-Robotics/TyrSharp/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Immortals-Robotics/TyrSharp/actions/workflows/dotnet.yml)

Tyr is the robotics software stack powering Immortals, our RoboCup Small-Size League team. It's built in modern C#, using a modular architecture with clean threading, pub-sub communication, real-time simulation, and a DVR-style replay system.

![Tyr GUI](Doc/gui.png)

## Building
TODO: fill

## Branching
We use [Github flow](https://docs.github.com/en/get-started/using-github/github-flow) as our branching strategy. Direct commits to the `main` branch are disabled, the goal is to keep it stable and usable.

<img src="https://www.gitkraken.com/wp-content/uploads/2021/03/git-flow.svg" width="300">

### Workflow
1. Create a new branch from main named `dev/your-awesome-dev-task`.
2. Commit changes to your new branch.
3. Open a pull request when you're done. 
4. After your PR is approved and all checks are passed, merge it into the main branch.
5. Delete your dev branch.

## License
This project is licensed under the terms of the GNU GPLv3.
