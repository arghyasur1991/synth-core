# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0] - 2026-03-04

### Added
- HumanoidAdapter pattern for multi-skeleton support with auto-detection (Daz Genesis 8, Mixamo, Generic fallback)
- SynthBone enum and SynthBoneCatalog for generic humanoid bone mapping (60+ bones including extended pectorals, gluteals)
- SynthBoneMapper: Avatar-first bone discovery with name-pattern fallback for non-standard skeletons
- SynthSetupWizard: full editor window for creating Synth prefabs from Humanoid models
- One-click Synth creation via right-click context menu on any Humanoid model in the Project window
- SynthCreationConfig: persists wizard settings per model for reproducible quick-create
- Core architecture: SynthEntity, SynthBrain, SynthMotorSystem, SynthProprioception, SynthVision, SynthAuditory, SynthContact
- MuJoCo integration: SynthPhysicalBody, SynthBoneJoints, SynthObservations, SynthActions, BoneFilterConfig
- Mesh splitting, eye camera generation, and body data with vertex-weight-based mass distribution
- Per-bone joint configuration with tuned defaults (SynthJointDefaults, SynthMassDefaults)
- Extensible skill architecture via ISynthSkill, ISynthSense, ISynthPlanner interfaces
- Zero-allocation observation pipeline for VR-compatible frame budgets
