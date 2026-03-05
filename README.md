# Synth Core

Create physics-simulated humanoids in Unity using [MuJoCo](https://mujoco.org/) for accurate rigid-body dynamics, mesh-based collision geometry, and an extensible skill architecture. Ships with built-in support for **Daz Genesis 8** and **Mixamo** skeletons, and is extensible to other Humanoid rigs via the adapter pattern.

## Features

- **Multi-Skeleton Support** — Built-in adapters for Daz Genesis 8 and Mixamo rigs with auto-detection; extensible to other skeleton types (including later Genesis versions) by adding a `HumanoidAdapter` subclass.
- **Editor Wizard** (`Synth > Create Synth from Humanoid`) — Guided setup: select model, review discovered bones, configure joints, set mass distribution, generate assets and prefab. Also available as a one-click right-click menu on any Humanoid model in the Project window.
- **Avatar-First Bone Discovery** — Uses Unity's Humanoid Avatar API to map standard bones, with name-pattern fallback for extended bones (pectorals, gluteals).
- **MuJoCo Physics** — Mesh-based collision geometry per bone, motor actuators with per-axis gear ratios, configurable spring stiffness and damping.
- **Extensible Skill Architecture** — `ISynthSkill`, `ISynthSense`, `ISynthPlanner` interfaces for building custom behaviors on top of the physics body.
- **Mass Distribution** — Automatic vertex-weight-based mass allocation with hierarchical parent/child redistribution.
- **Eye Cameras** — Stereo vision cameras positioned at eye bones for first-person visual observations.

## Ecosystem

synth-core is the foundation of a three-package architecture for creating, training, and interacting with physics-simulated humanoids:

| Package | Role | |
|---------|------|-|
| **synth-core** *(this repo)* | Humanoid creation, MuJoCo physics, skill architecture | — |
| [**synth-training**](https://github.com/arghyasur1991/synth-training) | On-device reinforcement learning via TorchSharp SAC | Optional |
| [**synth-vr**](https://github.com/arghyasur1991/synth-vr) | Mixed reality interaction on Meta Quest | Optional |

synth-core provides the physics body, motor system, and extensible skill/sense interfaces. **synth-training** plugs into these interfaces (`ISynthSkill`) to add on-device learning — no external Python server needed. **synth-vr** adds Quest hand tracking, room integration, and passthrough rendering so the Synth lives in your physical space. When all three are combined, the Synth trains live on Quest while you physically interact with it in your room.

## Requirements

- Unity 6000.x or later
- MuJoCo Unity plugin (`org.mujoco`) — via the [arghyasur1991/mujoco](https://github.com/arghyasur1991/mujoco) fork (`synth-patches` branch)
- `pip install mujoco` for the native library on macOS/Linux

## Installation

Add to your Unity project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.genesis.synth": "https://github.com/arghyasur1991/synth-core.git",
    "org.mujoco": "https://github.com/arghyasur1991/mujoco.git?path=unity#synth-patches"
  }
}
```

Or clone as local submodules and reference them:

```json
{
  "dependencies": {
    "com.genesis.synth": "file:../../synth-core",
    "org.mujoco": "file:../../mujoco/unity"
  }
}
```

## Quick Start

1. Import a Humanoid character model into Unity (Daz Genesis 8 or Mixamo recommended; set Rig type to **Humanoid** in import settings).
2. Right-click the model in the Project window and select **Create Synth** for one-click setup with defaults, or open **Synth > Create Synth from Humanoid** for the full wizard.
3. Drop the generated prefab into your scene.
4. Press Play — the character is now a physics-simulated Synth.

## Package Structure

```
synth-core/
├── Runtime/
│   ├── Core/              SynthBone, SynthBoneCatalog, SynthBoneMapper,
│   │                      HumanoidAdapter, Adapters/ (Daz, Mixamo, Generic)
│   ├── Components/        SynthEntity, SynthBrain, SynthMotorSystem, Senses
│   ├── Data/              SynthData, SynthModel, SynthBonesData, SynthBodyData,
│   │                      BoneJointsData, SynthJointDefaults, SynthMassDefaults
│   ├── MujocoComponents/  SynthPhysicalBody, SynthBoneJoints, SynthObservations,
│   │                      SynthActions, BoneFilterConfig
│   ├── Interfaces/        ISynthSkill, ISynthSense, ISynthPlanner
│   ├── Mesh/              SynthMeshRenderers, SynthEyeCameras
│   └── Utility/           TransformExtensions
└── Editor/
    ├── SynthSetupWizard.cs
    ├── Inspectors/        Custom editors for SynthModel, BoneJointsData, SynthBodyData
    ├── SynthAutoRebind.cs
    └── PlayModeSelectionFix.cs
```

## API Overview

| Type | Purpose |
|------|---------|
| `SynthEntity` | Root identity component — declares "this is a Synth" |
| `SynthBrain` | Coordinates skills and motor control each physics step |
| `SynthMotorSystem` | MuJoCo ctrl callback, action application, observation collection |
| `SynthBoneMapper` | Avatar-based bone discovery with name-pattern fallback |
| `ISynthSkill` | Interface for implementing behaviors (training, imitation, etc.) |
| `ISynthSense` | Interface for sensory modalities (proprioception, vision, contact, auditory) |
| `SynthData` | ScriptableObject aggregating model, joints, and body data |
| `BoneFilterConfig` | Configures which bones contribute to observations and actions |

## Roadmap

- **Real senses** — Vision from eye cameras, auditory processing, contact/touch perception — replacing current stubs with functional sensory pipelines
- **Multi-Synth scenes** — Multiple independent Synths in one MuJoCo scene with inter-character contact and social dynamics
- **Speech and communication** — Talking to and with the Synth through natural language, voice synthesis, and conversational interaction
- **Higher-level brain** — Task planning, goal decomposition, and executive function layers on top of the motor skill architecture
- **Toward sentience** — Long-term goal: autonomous agents with integrated perception, memory, reasoning, and self-directed behavior within virtual worlds

## License

Apache-2.0 — see [LICENSE](LICENSE) for details.
