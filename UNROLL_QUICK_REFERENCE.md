# Unroll Curved Surface - Quick Reference Guide

## Quick Start (3 Steps)

### 1️⃣ Launch Command
Click **"Unroll Curved Surface"** in the EFFE ribbon panel

### 2️⃣ Select Geometry
- Click **"Select Geometry"**
- Pick faces (preferred) or edges from your model
- ESC to finish selection

### 3️⃣ Choose View & Process
- **New View**: Enter name → Click Process
- **Existing View**: Select from dropdown → Click Process

---

## Supported Geometry ✅

| Type | Example | Status |
|------|---------|--------|
| **Cylindrical** | Pipes, columns, round ducts | ✅ Fully Supported |
| **Conical** | Tapered elements, funnels | ✅ Fully Supported |
| **Planar** | Flat walls, floors | ✅ Fully Supported |
| **Ruled** | Simple curved panels | ✅ Supported |
| **Freeform/NURBS** | Complex organic shapes | ❌ Not Supported |

---

## Common Use Cases

### 🔧 Fabrication
Flatten pipe sections, ducts, or curved panels for cutting templates

### 📐 Documentation
Create 2D elevation patterns from curved building elements

### 📋 Shop Drawings
Generate flat patterns for metal fabrication

### 🎨 Design Development
Visualize how curved surfaces will appear when flattened

---

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **ESC** | Cancel selection |
| **Enter** | Accept/Process (when Process button focused) |
| **Tab** | Navigate between fields |

---

## Tips & Tricks 💡

### ✨ Select Multiple Faces
- Hold **Ctrl** while selecting to add multiple faces
- They'll be automatically spaced in the drafting view

### 🎯 Best Results
- Use clean, simple curved geometry
- Cylindrical and conical faces work best
- Avoid highly complex NURBS surfaces

### 📏 Accurate Measurements
- All lengths are preserved during unrolling
- Arc lengths become straight lines
- Height/depth dimensions remain accurate

### 🔄 Multiple Attempts
- If first attempt fails, try selecting fewer faces
- Break complex geometry into simpler sections
- Use edge selection as fallback

---

## Troubleshooting 🔍

| Problem | Solution |
|---------|----------|
| **"Face type not supported"** | Try simpler cylindrical or conical faces |
| **No filled region types** | Create a filled region type in Revit first |
| **Geometry distorted** | Use cylindrical faces instead of complex ruled surfaces |
| **Faces overlap** | Manually adjust spacing in drafting view after creation |

---

## Output

The tool creates:
- ✅ **Filled Region** in the selected drafting view
- ✅ **2D boundary curves** representing the flattened geometry
- ✅ **Automatically switches** to show the result view

---

## Example Workflow: Pipe Section

```
1. Model: 10' long cylindrical pipe, 12" diameter
   ↓
2. Select: Face of the pipe
   ↓
3. Process: Creates flat pattern in drafting view
   ↓
4. Result: Rectangle ~37.7" wide (circumference) × 10' tall
```

---

## Feature Compatibility

- ✅ Revit 2022, 2023, 2024, 2025, 2026
- ✅ Windows 10/11
- ✅ Works with most model categories
- ✅ Compatible with all Revit templates

---

## Need Help?

📧 Contact support or file an issue
📖 Read full documentation: `UNROLL_FEATURE_DOCUMENTATION.md`
💻 Check code examples in documentation

---

## Version: 1.0.0

**Last Updated:** February 2026
