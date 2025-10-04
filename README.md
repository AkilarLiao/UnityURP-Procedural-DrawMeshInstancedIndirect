# UnityURP-Procedural-DrawMeshInstancedIndirect
Procedural grass & vegetation rendering in Unity URP using **DrawMeshInstancedIndirect** with **FilterMap** artist-editable mask control.  

Unlike many "Mobile DrawMeshInstancedIndirect" examples, this project is truly **mobile-friendly**:  
it avoids SSBO (StructuredBuffer) and uses **RenderTexture** to store results, making it compatible with a wide range of Android devices.

---

![Performance Example](Documents/Performance_S10.jpg)

You can directly test the demo on Android devices using the APK below:  
[Download UnityURP-Procedural-DrawMeshInstancedIndirect.apk (Google Drive)](https://drive.google.com/file/d/1JvjxpCMMo1-mqA5WGcuJlwyK9vAk2k02/view?usp=sharing)

## Live Preview (Supports Forward+)

![Procedural Grass Demo](Documents/UnityURP-Procedural-DrawMeshInstancedIndirect_Large.gif)

## Demo Video

[![Demo Video](https://img.youtube.com/vi/7fhobV4y3yY/0.jpg)](https://www.youtube.com/watch?v=7fhobV4y3yY)

---

## ✨ Features

- **Procedural instance generation** — no pre-generated assets or transform buffers  
- **FilterMap editing support** — paint distribution masks directly in the editor  
- **WeightMap multi-color control** — different colors = different vegetation types  
- **DrawMeshInstancedIndirect pipeline** — maximum GPU efficiency for large-scale rendering  
- Verified on **mid-range Android devices** (Galaxy S10 shown above)

---

## 📱 Why Mobile-Friendly?

Many existing "Mobile DrawMeshInstancedIndirect" examples rely on **StructuredBuffers (SSBO)**  
to store instance data. This works on PC and some high-end devices, but **fails on many Android phones**  
(e.g., Samsung Galaxy S10).  

This project is designed for **broad mobile compatibility**:  

- Stores results in **RenderTexture** (not SSBO)  
- Runs smoothly on **mid-range Android hardware**  
- Avoids PC-only assumptions — every feature has been validated on real devices  

If your goal is to support **the majority of mobile devices**,  
this design choice makes a critical difference.

---

## 🎨 Why FilterMap?

Traditional approaches often require either:
- Pre-generated transform arrays (huge memory cost)  
- Manual placement (time-consuming for artists)  

With **FilterMap**, distribution becomes **procedural + editable**:  
- Artists paint masks (brush tools, multiple colors)  
- The GPU generates variation & randomness  
- No manual placement required, instant feedback in-editor  

This workflow bridges **procedural generation** with **artistic direction**.

---

## 📖 Learn More

[Read the full technical article on Google Blog](https://makedreamvsogre.blogspot.com/2025/09/building-cross-platform-gpu-procedural.html)

---

## 🔍 Looking for a Complete Solution?

If you need a ready-to-use, production-level system with:

- Powerful **Compute Shader brush tools** for painting vegetation  
- Automatic Ground(Any Mesh) height & normal alignment  
- Support for four vegetation types (billboard-based)  
- Built-in wind animation and large-scale character interaction  
- User-friendly Unity Editor integration  

Check out [GPUPlantPainter on the Unity Asset Store](https://assetstore.unity.com/packages/tools/painting/gpuplantpainter-266965).  

For advanced workflows with **procedural random curve vegetation effects (Ghost of Tsushima style)**,  
see: [GPUGrassBladePainter on the Unity Asset Store](https://assetstore.unity.com/packages/tools/painting/gpugrassbladepainter-316417).