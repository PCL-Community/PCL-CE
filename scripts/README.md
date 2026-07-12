# Local plugin UI testing

Clone the private `PCL.Plugin` repository into `PCL.Plugin/`, then use one of these commands from the repository root.

Run the real Avalonia UI with the locally built plugin embedded:

```powershell
.\scripts\run-plugin-ui.ps1
```

Run only the injected-plugin UI flow through Avalonia Headless:

```powershell
.\scripts\test-plugin-ui.ps1
```

Both scripts accept `-PluginProject <path>` when the private repository is stored elsewhere. The headless test verifies that the plugin HostModule adds the navigation item and that its settings page renders the registered heading, description, and hints.
