# MapNavigatorDemo Scripts Organization

This document outlines the organizational structure of the scripts in the MapNavigatorDemo project.

## Folder Structure

```
Assets/Scripts/
├── Core/                          # Core navigation system
│   ├── RouteManager.cs            # High-level route management interface
│   ├── RouteNavigator.cs          # Main navigation orchestrator
│   ├── DirectionDetectionUtil.cs  # Direction analysis between route and player movement
│   ├── SpeedTrackingUtil.cs       # Player speed tracking with outlier detection
│   └── README.md
├── Controllers/                   # Input and interaction controllers
│   ├── CameraDragController.cs    # Camera movement and zoom control
│   ├── TargetPositionController.cs # Target setting and interaction
│   └── README.md
├── ExternalDriverSimulation/      # External driver/GPS simulation
│   ├── RouteSimulator.cs          # Simulates driver movement
│   ├── RouteRandomizer.cs         # Simulates route changes
│   └── README.md
├── Map/                           # Map display and spatial components
│   ├── MapTileGetter.cs           # Map tile management
│   ├── TileVisibilityDetector.cs  # Tile visibility optimization
│   └── README.md
├── Services/                      # External services and APIs
│   ├── OSRMService.cs             # OSRM API service
│   └── README.md
├── UI/                            # User interface components
│   ├── GenericPopup.cs            # Reusable popup system
│   ├── LoadingScreen.cs           # Loading screen management
│   ├── TargetPopupUI.cs           # Target-specific popup
│   ├── PositionMarker.cs          # Position marker UI component
│   └── README.md
└── README.md                      # This file
```

## Architectural Principles

### Separation of Concerns
Each folder represents a distinct area of functionality:
- **Core**: Central orchestration and main business logic
- **Controllers**: User input handling and interaction management
- **ExternalDriverSimulation**: Simulation of external data sources
- **Map**: Spatial data and map visualization
- **Services**: External API integration and shared services
- **UI**: User interface and presentation layer

### Dependency Flow
The intended dependency flow follows these guidelines:
- **Core** can depend on all other modules
- **RouteManager** centralizes UI and route references, manages RouteNavigator, and provides comprehensive visualization and tracking features:
  - Dynamic RouteNavigator creation and configuration
  - Real-time off-route detection with efficient segment tracking
  - Four-point visualization system: A/B (route segments), C/D (player history)
  - Configurable tracking parameters and visualization options
  - Player speed tracking with outlier detection and statistical analysis
  - Direction detection between route segments and player movement
- **Controllers** can depend on Core (especially RouteManager), Services, and UI
- **UI** components should be self-contained with minimal dependencies
- **Services** should be independent and reusable
- **Map** components focus on spatial functionality
- **ExternalDriverSimulation** simulates external inputs via events (easily replaceable)

### Benefits of This Organization

1. **Maintainability**: Related functionality is grouped together
2. **Scalability**: New features can be added to appropriate folders
3. **Testability**: Clear separation makes unit testing easier
4. **Modularity**: Components can be modified independently
5. **Clarity**: Purpose of each script is clear from its location
6. **Replaceability**: External simulation can be easily swapped for real data
7. **Single Configuration Point**: RouteManager provides one place to configure all routing aspects
8. **Dynamic Creation**: No need to manually place RouteNavigator in scenes

## Development Guidelines

- Keep scripts focused on a single responsibility
- Use the README files in each folder to document changes
- Consider dependencies when adding new scripts
- External simulations should be easily replaceable with real implementations
- UI components should be reusable where possible
