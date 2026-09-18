# SPH CaL Array RMS — Control

Experimental sequence-control software for arrayed electrified carbon capture.

**Platform:** Windows · **Language:** C# · **UI:** WPF · **Framework:** .NET 8 · **License:** MIT

## Overview

SPH CaL Array RMS (Reactor Management System) provides a modular interface for programming valve switching, electrical heating, delays, and repeated process sequences in the SPH calcium-looping (CaL) platform.

Electrified fixed-bed CaL requires frequent transitions between CO2 capture by carbonation and electrically heated regeneration. These transitions require coordinated switching of both inlet and outlet flow paths together with the heating mode. This project provides the experimental sequence-control layer for that operation.
<img width="3053" height="3053" alt="Figure 4-26V2" src="https://github.com/user-attachments/assets/4b8ab5c9-0a76-4482-b19b-37adc786611a" />
The broader array concept uses **time-division multiplexing of a shared electrical supply**: regeneration power is allocated sequentially among reactors instead of providing a dedicated supply for every reactor. With suitable cycle timing and switching hardware, this can reduce power-equipment cost and support a smaller system footprint and higher system-level space–time yield. These are system-design objectives, not performance guarantees provided by the software.

<img width="1800" height="1200" alt="7508f9ec1dbe96bb0e8cfd08cdea4a19" src="https://github.com/user-attachments/assets/606f4cbf-0f7c-41b6-8c3e-3f28cbe74473" />

> **Scope:** A 4 × 4 array is an illustrative configuration, not the only intended array size. The current implementation exposes **four heater channels and eight logical valve channels**; it does not by itself provide independent control of 16 complete reactors or arbitrary N × M arrays. The demonstration shows **inlet-valve switching only**; practical operation requires switching both inlet and outlet paths.

## Contents

- [Features](#features)
- [Requirements](#requirements)
- [Getting started](#getting-started)
- [Hardware configuration](#hardware-configuration)
- [Usage](#usage)
- [Process modules](#process-modules)
- [Array concept and implementation scope](#array-concept-and-implementation-scope)
- [Project structure](#project-structure)
- [Safety and limitations](#safety-and-limitations)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [Citation](#citation)
- [License](#license)

## Features

- **Module-based process editor:** arrange valve, heater, delay, and loop operations in execution order.
- **Valve control:** open or close eight logical channels.
- **Heater commands:** select one of four channels and configure power, On/Off action, and duration.
- **Repeated execution:** nested local loops and a global loop for the complete sequence.
- **Execution monitoring:** active step, elapsed time, estimated remaining time, estimated completion time, and global-loop index.
- **Process-file handling:** save and load sequences as JSON.
- **Hardware communication:** serial relay control through Modbus RTU and a heater-parameter extension frame.

## Requirements

### Development

- Windows 10 or Windows 11
- .NET 8 SDK
- Visual Studio 2022 with WPF/.NET desktop development support, or a compatible .NET development environment

### Hardware operation

- Compatible valve and heater relay controllers
- Available serial ports with the correct assignments
- Compatible heater-control electronics and verified relay wiring
- Independent hardware safety protection

The build restores the following NuGet dependencies:

| Package | Version |
| --- | --- |
| NModbus | 3.0.81 |
| NModbus.Serial | 3.0.81 |
| System.IO.Ports | 10.0.0 |
| Newtonsoft.Json | 13.0.4 |

## Getting started

Download or clone this repository and open a terminal in its root directory. Before operating physical hardware, complete [Hardware configuration](#hardware-configuration) and review [Safety and limitations](#safety-and-limitations).

### Build and run

Run the following commands in PowerShell:

```powershell
dotnet restore "SPH CaL Array RMS.sln"
dotnet build "SPH CaL Array RMS.sln" -c Release
dotnet run --project "SPHCaLArrayRMS.App/SPHCaLArrayRMS.App.csproj" -c Release
```

Alternatively, open `SPH CaL Array RMS.sln` in Visual Studio, set `SPHCaLArrayRMS.App` as the startup project, restore packages, and build the solution.

### Publish for Windows x64

To generate a self-contained build:

```powershell
dotnet publish "SPHCaLArrayRMS.App/SPHCaLArrayRMS.App.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false -o publish/win-x64
```

The output is written to `publish/win-x64`. Keep the published files together when distributing the application.

## Hardware configuration

### Serial connections

| Controller | Default port | Function |
| --- | --- | --- |
| Valve relay board | `COM9` | Valve switching |
| Heater relay board | `COM10` | Heater switching and parameter transmission |

Port assignments are defined in `SPHCaLArrayRMS.Core/Services/HardwareContext.cs`. Update the source and rebuild if your hardware uses different ports.

Both serial connections use **38400 baud, 8 data bits, no parity, and 1 stop bit**.

### Valve mapping

| Logical channel | Valve-board relay |
| --- | --- |
| D1 | Y1 |
| D2 | Y2 |
| D3 | Y3 |
| D4 | Y4 |
| U1 | Y5 |
| U2 | Y6 |
| U3 | Y7 |
| U4 | Y8 |

Verify the physical valve connected to each output before use. Channel names alone do not establish a safe gas-path configuration.

### Communication protocol

Standard relay commands use Modbus RTU slave address `1` with single-coil writes.

Heater parameters are sent on the heater-board connection to slave address `2`, using function code `0x10` to write two registers together:

| Register | Value |
| --- | --- |
| `0x0081` | Heater channel |
| `0x0082` | Heater power in watts |

The extension frame is written directly to the serial port **without waiting for an acknowledgement from slave address `2`**. Successful transmission does not confirm that the heater accepted or applied the parameters.

Heater switching uses **Y7 on the heater relay board**, which is distinct from Y7 on the valve relay board.

## Usage

1. Verify serial-port assignments, wiring, relay mappings, and independent safety protections.
2. Launch the application.
3. Create a process sequence in the module editor or load a trusted JSON process file.
4. Arrange valve commands, heater commands, and waits in the required execution order.
5. Add local loops and set the global-loop count as needed.
6. Review the complete sequence, including explicit heater-off commands and the required final valve states.
7. Save the process file and start execution.
8. Monitor the active step, timing estimates, and global-loop index.

Before starting, the software checks the hardware connections required by the sequence: valve commands require the valve board, and heater commands require the heater board. Connection checks do not validate the physical process or prove safe operating conditions.

### Process files

Use the interface to save and load JSON sequences. Treat these files as trusted local inputs; preferably generate them with the application.

### Stopping a sequence

**Stop** cancels execution and attempts to disable heater relay Y7. It does **not** automatically reset valve outputs; valves remain in their most recently commanded states. Verify the hardware state independently after stopping.

## Process modules

| Module | Configuration | Behavior |
| --- | --- | --- |
| Valve Control | Logical channel; Open/Close | Commands a valve-board relay |
| Heater Control | Channel 1–4; integer power in watts; On/Off; duration | Sends heater commands, then waits for the configured duration |
| Wait | Non-negative duration | Delays execution without changing outputs |
| Loop Start / Loop End | Local repeated block | Repeats the enclosed modules; nesting is supported |
| Global Loop | Whole-sequence repetition setting | Repeats the complete process |

Heater and Wait durations are stored with **0.01 s resolution**. This storage resolution is not a guarantee of hardware timing accuracy.

For a Heater Control **On** command, the application transmits the channel and power setpoint, enables heater-board Y7, and then waits for the configured duration. For **Off**, it disables Y7 and then waits.

> A heater module's duration does not imply automatic shutoff at its end. Include an explicit **Off** command where required. A Wait module leaves the existing heater and valve states unchanged.

## Array concept and implementation scope

### Example: 4 × 4 array

A conceptual 4 × 4 array contains reactors `R01`–`R16`. Staggered operation assigns reactors to capture, regeneration, or standby while a shared supply serves the selected regeneration load.

| Conceptual state | Gas routing | Heating |
| --- | --- | --- |
| Capture | Feed and capture-outlet paths connected | Off |
| Regeneration | Regeneration and CO2-product paths selected | Pulsed electrical heating |
| Standby / transition | Defined by the validated switching procedure | Off |

In the full system concept, inlet/outlet switching and power assignment must be coordinated. For clarity, the demonstration visualizes only inlet switching.

### Scaling beyond the example

The intended N × M architecture separates reactor-state management, gas-path scheduling, and shared-power scheduling. Array dimensions are a design choice, not a fixed 4 × 4 requirement.

The current release supplies configurable sequential commands, not a complete autonomous array scheduler. Expansion requires:

- additional valve and heater addressing, with a verified reactor-to-channel mapping;
- inlet and outlet switching hardware;
- a power-switching network compatible with the supply and reactor loads;
- scheduling logic that respects regeneration demand and available power; and
- validated interlocks, switching dead times, feedback, and fault handling.

Sharing a power supply does not inherently shrink an individual reactor or improve its intrinsic reaction rate. System-level volume and space–time-yield benefits must be evaluated using a stated volume basis, actual cycle times, throughput, and auxiliary hardware.

## Project structure

| Path | Responsibility |
| --- | --- |
| `SPH CaL Array RMS.sln` | Visual Studio solution |
| `SPHCaLArrayRMS.App/` | WPF interface, process editing, timing display, and file handling |
| `SPHCaLArrayRMS.App/ViewModels/` | User-interface view models |
| `SPHCaLArrayRMS.Core/Models/` | Process models |
| `SPHCaLArrayRMS.Core/Services/` | Execution, communication, and supporting services |
| `SPHCaLArrayRMS.Core/Services/HardwareContext.cs` | Hardware context and serial-port assignments |

## Safety and limitations

This is research software, not a certified industrial control or safety system.

- Use independent emergency shutdown, electrical isolation, and appropriate temperature, current, and pressure protection.
- Validate startup, normal operation, transitions, shutdown, and fault states before energizing the system.
- Do not rely on the software Stop command to establish a safe valve configuration.
- Do not interpret a sent command or displayed step as confirmed physical actuation.
- Validate heater parameters and timing against the connected hardware.
- Reproduction requires the reactor, gas manifold, switching electronics, interlocks, and operating conditions in addition to this software.

## Troubleshooting

| Symptom | Checks |
| --- | --- |
| Required hardware is reported as disconnected | Verify board power, USB/serial connection, assigned COM port, and whether another application is using the port |
| Valve does not respond as expected | Check valve-board wiring, logical-channel mapping, serial settings, and slave address |
| Heater relay switches but parameters are not applied | Check support for slave address `2`, function `0x10`, and registers `0x0081`–`0x0082`; the application does not wait for extension-frame acknowledgement |
| Valves retain their state after Stop | This is the documented behavior; use the independently validated shutdown procedure |
| Build or package restore fails | Confirm the .NET 8 SDK and Windows development environment, then inspect the build output and package-restoration errors |

## Contributing

Bug reports and improvements can be submitted through this repository's Issues and pull requests, where enabled.

For a bug report, include the software version or commit, Windows and .NET versions, relevant hardware configuration, reproduction steps, and expected versus observed behavior. Remove sensitive information from logs and process files.

For changes affecting hardware control, describe changes to channel mappings, timing, protocol behavior, shutdown behavior, and safety assumptions. State whether validation used actual hardware or software-only checks.

## Citation

If this software contributes to your research, cite the repository and the exact release version or commit used. The associated article citation and DOI will be added after publication.

## License

Released under the [MIT License](LICENSE).
