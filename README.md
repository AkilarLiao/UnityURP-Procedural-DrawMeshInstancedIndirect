# UnityURP-Procedural-DrawMeshInstancedIndirect
A minimal, professional implementation of procedural DrawMeshInstancedIndirect rendering in Unity URP.  
Demonstrates how to generate instance data entirely at runtime and use a WeightMap to control grass distribution — no asset preloading required, all data generated on the GPU.
---

## Features

- Purely procedural instance data generation (no pre-generated assets)
- WeightMap-based distribution control (e.g., for grass/vegetation)
- DrawMeshInstancedIndirect rendering pipeline for maximum efficiency

## Looking for a Complete Solution?

If you need a ready-to-use, production-level system with:

- Powerful Compute Shader brush tools for painting vegetation
- Automatic terrain height & normal alignment
- Support for four types of vegetation (billboard-based)
- Built-in wind system and large-scale character interaction (push & flatten grass)
- User-friendly editor integration

Check out [GPUPlantPainter on the Unity Asset Store](https://assetstore.unity.com/packages/tools/painting/gpuplantpainter-266965).
