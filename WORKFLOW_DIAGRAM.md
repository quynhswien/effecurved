# Unroll Curved Surface - Workflow Diagram

## User Workflow

```
┌─────────────────────────────────────────────────────────────┐
│                    START: User Action                       │
│                  Click Ribbon Button                        │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Command Execution Layer                        │
│         UnrollCurvedSurfaceCommand.Execute()                │
│                                                             │
│  ✓ License Check (via BaseCommand)                         │
│  ✓ Statistics Recording                                    │
│  ✓ Error Handling Setup                                    │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                  UI Layer (WPF)                             │
│          UnrollCurvedSurfaceView Dialog                     │
│                                                             │
│  ┌───────────────────────────────────────────────┐         │
│  │  1. Select Geometry                           │         │
│  │     [Select Geometry Button]                  │         │
│  │                                               │         │
│  │  2. Choose Target View                        │         │
│  │     ○ Create New View: [_____________]        │         │
│  │     ○ Use Existing: [Dropdown ▼]             │         │
│  │                                               │         │
│  │  3. Action                                    │         │
│  │     [Process]  [Cancel]                      │         │
│  └───────────────────────────────────────────────┘         │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              ViewModel Layer (MVVM)                         │
│        UnrollCurvedSurfaceViewModel                         │
│                                                             │
│  Implements IExternalEventHandler                          │
│  Manages UI state and Revit API calls                      │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          User Selection (Revit Context)                     │
│                                                             │
│  ┌────────────────────┐                                    │
│  │ Select Geometry    │                                    │
│  │ via PickObjects()  │                                    │
│  └─────────┬──────────┘                                    │
│            │                                                │
│            ├──► Try Faces First                            │
│            │    (ObjectType.Face)                          │
│            │                                                │
│            └──► Fallback to Curves/Edges                   │
│                 (ObjectType.Edge)                          │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          Service Layer - Geometry Analysis                  │
│              GeometryUnrollService                          │
│                                                             │
│  For Each Selected Face:                                   │
│  ┌──────────────────────────────────────────┐             │
│  │ 1. Classify Face Type                    │             │
│  │    • IsCylindricalFace() → Cylinder      │             │
│  │    • IsConicalFace() → Cone              │             │
│  │    • IsPlanarFace() → Plane              │             │
│  │    • IsRuledSurface() → Ruled Surface    │             │
│  └──────────────┬───────────────────────────┘             │
│                 │                                          │
│                 ▼                                          │
│  ┌──────────────────────────────────────────┐             │
│  │ 2. Apply Appropriate Algorithm           │             │
│  │    ↓                                      │             │
│  │    UnrollCylindricalFace()               │             │
│  │    UnrollConicalFace()                   │             │
│  │    UnrollPlanarFace()                    │             │
│  │    UnrollRuledSurface()                  │             │
│  └──────────────┬───────────────────────────┘             │
│                 │                                          │
│                 ▼                                          │
│  ┌──────────────────────────────────────────┐             │
│  │ 3. Generate 2D Curves                    │             │
│  │    • Extract edge loops                  │             │
│  │    • Tessellate curves                   │             │
│  │    • Map 3D → 2D coordinates             │             │
│  │    • Create line segments                │             │
│  └──────────────┬───────────────────────────┘             │
└─────────────────┴───────────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│           Drafting View Management                          │
│                                                             │
│  ┌─────────────────┐        ┌─────────────────┐           │
│  │ Create New View │   OR   │ Use Existing    │           │
│  │                 │        │ View            │           │
│  │ • Get view type │        │ • Validate view │           │
│  │ • Create view   │        │ • Get view ID   │           │
│  │ • Set name      │        │                 │           │
│  └────────┬────────┘        └────────┬────────┘           │
│           │                          │                     │
│           └──────────┬───────────────┘                     │
│                      │                                     │
│                      ▼                                     │
│           ┌────────────────────┐                          │
│           │ Target View Ready  │                          │
│           └──────────┬─────────┘                          │
└──────────────────────┴──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          Transaction & Element Creation                     │
│                                                             │
│  using (Transaction trans = new Transaction(doc))          │
│  {                                                          │
│      trans.Start("Unroll Curved Surface");                 │
│                                                             │
│      ┌────────────────────────────────────┐               │
│      │ 1. Create CurveLoop from curves    │               │
│      └────────────┬───────────────────────┘               │
│                   │                                        │
│                   ▼                                        │
│      ┌────────────────────────────────────┐               │
│      │ 2. Get FilledRegionType            │               │
│      └────────────┬───────────────────────┘               │
│                   │                                        │
│                   ▼                                        │
│      ┌────────────────────────────────────┐               │
│      │ 3. Create FilledRegion             │               │
│      │    FilledRegion.Create()           │               │
│      └────────────┬───────────────────────┘               │
│                   │                                        │
│                   ▼                                        │
│      trans.Commit();                                       │
│  }                                                          │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                  Post-Processing                            │
│                                                             │
│  ✓ Set active view to drafting view                        │
│  ✓ Record statistics                                       │
│  ✓ Update UI with success message                          │
│  ✓ Close dialog                                            │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                      RESULT                                 │
│                                                             │
│  User sees:                                                 │
│  • Drafting view with unrolled geometry                    │
│  • Filled region representing flattened surface            │
│  • View is active and ready for annotation                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Geometry Processing Detail

### Cylindrical Face Processing

```
3D Cylindrical Face
    │
    ├─► Extract: Axis, Origin, Radius
    │
    ├─► Get Edge Loops
    │
    ├─► For Each Point on Edge:
    │   │
    │   ├─► Project onto axis → Height (Y)
    │   ├─► Calculate radial vector
    │   ├─► Calculate angle from reference
    │   └─► Map to 2D: X = radius × angle, Y = height
    │
    └─► Connect points → 2D Curves
            │
            └─► Result: Rectangular pattern
```

### Conical Face Processing

```
3D Conical Face
    │
    ├─► Extract: Axis, Origin, Half Angle
    │
    ├─► Get Edge Loops
    │
    ├─► For Each Point on Edge:
    │   │
    │   ├─► Calculate height along axis
    │   ├─► Calculate slant height
    │   ├─► Calculate angle around cone
    │   ├─► Scale angle for development
    │   └─► Convert to Cartesian 2D
    │
    └─► Connect points → 2D Curves
            │
            └─► Result: Sector/fan pattern
```

---

## Data Flow

```
┌──────────┐
│   User   │
└────┬─────┘
     │ Interaction
     ▼
┌──────────────┐
│  UI (View)   │◄──────┐
└────┬─────────┘       │
     │ Commands        │ Property Updates
     ▼                 │
┌──────────────┐       │
│  ViewModel   │───────┘
└────┬─────────┘
     │ ExternalEvent
     ▼
┌──────────────┐
│ Revit API    │
│  (Context)   │
└────┬─────────┘
     │ Transactions
     ▼
┌──────────────┐
│   Service    │
│  (Business   │
│    Logic)    │
└────┬─────────┘
     │ Results
     ▼
┌──────────────┐
│   Revit      │
│  Document    │
└──────────────┘
```

---

## Error Handling Flow

```
┌─────────────────────┐
│  Any Operation      │
└──────────┬──────────┘
           │
           ├─► Success
           │   │
           │   └─► Continue to next step
           │
           └─► Error
               │
               ├─► Catch Exception
               │   │
               │   ├─► Log Error
               │   ├─► Record Statistics
               │   ├─► Rollback Transaction (if open)
               │   └─► Show User-Friendly Message
               │
               └─► User Decision
                   │
                   ├─► Retry (if applicable)
                   └─► Cancel Operation
```

---

## Threading Model

```
┌───────────────────┐
│   UI Thread       │
│                   │
│  • User clicks    │
│  • Dialog shows   │
│  • Properties     │
│    update         │
└─────────┬─────────┘
          │
          │ ExternalEvent.Raise()
          │
          ▼
┌───────────────────┐
│  Revit Thread     │
│                   │
│  • Selection      │
│  • Transactions   │
│  • API Calls      │
│  • Geometry       │
│    processing     │
└─────────┬─────────┘
          │
          │ Dispatcher.Invoke()
          │
          ▼
┌───────────────────┐
│   UI Thread       │
│                   │
│  • Update status  │
│  • Show results   │
│  • Close dialog   │
└───────────────────┘
```

---

## Component Interaction

```
┌─────────────────────────────────────────────────────────┐
│                    Application.cs                       │
│  • Registers commands in ribbon                         │
│  • Initializes services                                 │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│              UnrollCurvedSurfaceCommand                 │
│  • Inherits from BaseCommand                            │
│  • Handles licensing, stats, errors                     │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│           UnrollCurvedSurfaceViewModel                  │
│  • Implements IExternalEventHandler                     │
│  • Coordinates all operations                           │
└────────────┬────────────────────┬────────────────────────┘
             │                    │
             │                    │
             ▼                    ▼
┌──────────────────────┐  ┌──────────────────────┐
│ GeometryUnrollService│  │  Revit API Services  │
│ • Face analysis      │  │  • Selection         │
│ • Unrolling logic    │  │  • Views             │
│ • Region creation    │  │  • Transactions      │
└──────────────────────┘  └──────────────────────┘
```

---

## Success Path Summary

```
1. User clicks button
   ↓
2. Dialog opens
   ↓
3. User selects faces
   ↓
4. User chooses view
   ↓
5. User clicks Process
   ↓
6. Geometry analyzed
   ↓
7. Curves unrolled
   ↓
8. View created/selected
   ↓
9. Filled region created
   ↓
10. View activated
    ↓
11. Success message
    ↓
12. Dialog closes
    ↓
DONE ✓
```

---

**This workflow ensures:**
- ✅ Proper Revit API context handling
- ✅ Thread-safe UI updates
- ✅ Comprehensive error handling
- ✅ Clean separation of concerns
- ✅ User-friendly experience
