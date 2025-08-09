# Dispensary Simulator
## Complete Project Documentation & Context

### 🎯 **Project Vision**
A **dispensary simulator game** built in Unity 3D, similar to supermarket simulator games. Features first-person perspective with authentic dispensary workflow where players serve customers behind a counter, manage inventory, and run a business. **Multiplayer-ready** using Unity Relay for cooperative gameplay.

---

## 📁 **Project Architecture**

### **Folder Structure**
```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Core/           # GameManager, SaveSystem
│   │   ├── Player/         # FirstPersonController, PlayerInteraction  
│   │   ├── Store/          # StoreManager, CashRegister, InventorySystem
│   │   ├── Products/       # Product scripts, ScriptableObject data
│   │   ├── Economy/        # MoneyManager, pricing systems
│   │   ├── UI/             # Game interfaces, inventory management
│   │   └── Multiplayer/    # RelayManager, network components
│   ├── Prefabs/
│   ├── ScriptableObjects/
│   ├── Scenes/
│   └── Materials/
```

### **Namespace Organization**
- `DispensarySimulator.Core` - Core game systems
- `DispensarySimulator.Player` - Player controls and interaction
- `DispensarySimulator.Store` - Store management systems
- `DispensarySimulator.Economy` - Money and economic systems
- `DispensarySimulator.Products` - Product data and management
- `DispensarySimulator.UI` - User interface systems

---

## 🎮 **Current Game State & Systems**

### **✅ Implemented & Working Systems**

#### **Core Game Management**
- **GameManager** - Handles game state, pausing, debug features
- **FirstPersonController** - WASD movement, mouse look, jumping with multiplayer-aware cursor management
- **PlayerInteraction** - Raycast-based interaction with "[E] Interact" prompts

#### **Economy System** 
- **MoneyManager** (NetworkBehaviour) - Multiplayer-synced economy
  - Starting money: $500
  - Shared cash register between all players
  - Daily earnings tracking with targets
  - Server authority for all money operations
  - Real-time sync across all clients

#### **Inventory & Store Management**
- **Tab-based inventory interface** for ordering wholesale products
- **Product System** - ScriptableObject-based products with pricing, stock management
- **StoreManager** - Inventory tracking, product placement on shelves
- **Store Integration** - Professional jewelry store assets

#### **Multiplayer Networking**
- **Unity Relay** integration for internet multiplayer
- **NetworkManager** setup with UnityTransport
- **Host/Join system** with join codes
- **RelayManager** - Handles multiplayer connection and UI management
- **Automatic UI state management** (menu/game/inventory modes)

### **Current Game Loop**
1. **Multiplayer Setup** → Host creates dispensary or client joins with code
2. **Start with $500** initial money (shared between players)
3. **Press Tab** → Browse wholesale product catalog with cursor
4. **Order inventory** → Spend shared money to stock store
5. **Products appear** on shelves automatically
6. **Serve customers** (next major feature) → Sell at retail prices
7. **Make profit** → Reinvest in more inventory

---

## 🎛️ **Controls & Input System**

### **Player Controls**
- **WASD** - Movement (works in all modes)
- **Mouse** - Look around (only in game mode)
- **Left Shift** - Run
- **Space** - Jump
- **E** - Interact with objects

### **UI Controls**
- **Escape** - Toggle main menu (multiplayer create/join)
- **Tab** - Toggle inventory/supplier catalog  
- **F1** - Quick disconnect (debug)

### **Cursor Management States**
- **Game Mode** - Cursor locked, mouse look active
- **Menu Mode** - Cursor visible, can interact with multiplayer UI
- **Inventory Mode** - Cursor visible, can purchase products
- **Smart State Switching** - Menu and inventory are mutually exclusive

---

## 🌐 **Multiplayer Implementation**

### **Technology Stack**
- **Unity Netcode for GameObjects** - Core networking
- **Unity Relay** - Internet multiplayer (no dedicated servers needed)
- **Unity Authentication** - Anonymous sign-in
- **Unity Transport** - Low-level networking

### **Network Architecture**
- **Client-Server Model** - Host acts as server with authority
- **Server Authority** - All money operations validated by server
- **NetworkVariables** - Automatic synchronization of money, inventory
- **ServerRPCs** - Client requests, server validates and updates

### **Multiplayer Features**
- **Internet multiplayer** via Unity Relay (no port forwarding needed)
- **Join codes** for easy connection sharing
- **Shared dispensary** - All players work in same store
- **Cooperative gameplay** - Shared money and inventory
- **Real-time sync** - Money and purchases sync instantly
- **Connection handling** - Automatic UI management on connect/disconnect

### **Testing Setup**
- **Build game** → Run built version as Player 1
- **Unity Editor** → Run as Player 2
- **Host creates dispensary** → Gets join code
- **Client enters code** → Joins same dispensary

---

## 📄 **Key Scripts Documentation**

### **FirstPersonController.cs**
```csharp
namespace DispensarySimulator.Player
```
- **Multiplayer-aware cursor management**
- **Three modes**: Game (cursor locked), Menu (cursor visible), Inventory (cursor visible)
- **Smooth movement** with WASD, mouse look, jumping
- **Audio system** with footstep sounds
- **Smart state transitions** between UI modes

**Key Methods:**
- `SetMenuMode(bool)` - Switch to/from main menu
- `SetInventoryMode(bool)` - Switch to/from inventory
- `IsInUIMode()` - Check if any UI is open

### **MoneyManager.cs**
```csharp
namespace DispensarySimulator.Economy : NetworkBehaviour
```
- **NetworkVariable<float>** for synchronized money
- **Server authority** - only server can modify money
- **Event system** for UI updates
- **Daily earnings tracking** with targets
- **Save/load functionality**

**Key Methods:**
- `SpendMoney(float)` - Purchase items (validates on server)
- `AddSaleEarnings(float)` - Revenue from sales
- `CanAfford(float)` - Check if sufficient funds

### **RelayManager.cs**
```csharp
public class RelayManager : MonoBehaviour
```
- **Unity Relay integration** for internet multiplayer
- **Host/Join functionality** with join codes
- **Smart UI management** - shows/hides based on connection state
- **Automatic cursor state management**
- **Connection event handling**

**Key Methods:**
- `CreateDispensary()` - Host a new game session
- `JoinDispensary()` - Join existing session with code
- `DisconnectFromDispensary()` - Leave multiplayer session

---

## 🚧 **Current Issues & Limitations**

### **Resolved Issues**
- ✅ **Cursor conflict** - Fixed with smart state management
- ✅ **Money synchronization** - Working with NetworkVariables
- ✅ **UI visibility** - Auto-managed based on connection state
- ✅ **Tab key functionality** - Inventory opens with cursor support

### **Known Limitations**
- **No customer AI** - Core feature still in development
- **Products don't persist** - Inventory resets on disconnect
- **Limited error handling** - Network failures could be more graceful
- **No save/load** - Game state not persistent yet

---

## 🎯 **Next Development Phases**

### **Phase 2: Customer System (Next Priority)**
- **Customer AI** - NPCs that approach counter and make orders
- **Order system** - Customers request specific products
- **Transaction flow** - Retrieve products, calculate total, take payment
- **Customer patience** - Time pressure for realistic simulation

### **Phase 3: Enhanced Store Management** 
- **Shelf restocking** - Manual product placement
- **Inventory alerts** - Notifications when stock is low
- **Product varieties** - Different strains, edibles, accessories
- **Store customization** - Shelving, decorations, layout

### **Phase 4: Business Simulation**
- **Daily/weekly cycles** - Time-based gameplay loops
- **Store progression** - Upgrades, expansions, new locations
- **Competition** - Other dispensaries affecting business
- **Reputation system** - Customer satisfaction affects traffic

### **Phase 5: Advanced Multiplayer**
- **Role specialization** - Cashier, stock manager, security
- **Multiple store locations** - Different players run different stores
- **Competitive modes** - Race for highest profits
- **Custom lobbies** - Named rooms instead of join codes

---

## 🛠️ **Technical Setup**

### **Unity Version**
- **Unity 2022.3 LTS** (recommended)
- **.NET Framework** compatible

### **Required Packages**
```
Unity Netcode for GameObjects
Unity Relay  
Unity Authentication
Unity Transport
TextMeshPro (for UI)
```

### **Unity Cloud Setup**
1. **Link project** to Unity Cloud (Window → Services)
2. **Enable Relay service** in Unity Dashboard
3. **Project ID** automatically configured

### **Assets Required**
- **Jewelry Store Pack** (already integrated)
- **Audio clips** for footsteps (optional)
- **Product ScriptableObjects** (framework ready)

---

## 🧪 **Testing Procedures**

### **Single Player Testing**
1. **Play scene** → Should start in menu mode
2. **Press Tab** → Inventory should open
3. **Press Escape** → Should switch between menu/game modes
4. **Movement** → WASD should work in all modes

### **Multiplayer Testing**
1. **Build project** → Create executable
2. **Run build** → Host player
3. **Unity Editor** → Client player  
4. **Host creates dispensary** → Note join code
5. **Client joins** → Enter join code
6. **Test money sync** → Both should see same amount
7. **Test Tab inventory** → Should work for both players

### **UI State Testing**
1. **Game start** → Menu visible, cursor unlocked
2. **Create/Join** → Menu hidden, cursor locked
3. **Press Tab** → Cursor unlocked for inventory
4. **Press Escape** → Appropriate menu shows based on connection

---

## 🏗️ **Build Instructions**

### **Development Build**
1. **File → Build Settings**
2. **Add current scene**
3. **Development Build** ✓ (for debugging)
4. **Build** to dedicated folder

### **Production Considerations**
- **Remove debug keys** (F1 disconnect)
- **Remove debug console logs**
- **Optimize networking** (reduce ServerRPC calls)
- **Add error handling** for network failures

---

## 📊 **Performance Considerations**

### **Networking**
- **NetworkVariables** update automatically (efficient)
- **ServerRPCs** only when needed (money transactions)
- **Client prediction** not implemented (could improve responsiveness)

### **Rendering**
- **First-person optimized** (no player model needed)
- **Jewelry store assets** are well-optimized
- **UI updates** only on value changes (not every frame)

---

## 🎮 **Gameplay Philosophy**

### **Cooperative Experience**
- **Shared ownership** - All players work toward same goal
- **Team coordination** - Must communicate for efficient operation
- **Shared consequences** - Poor decisions affect everyone
- **Collective success** - Profits benefit entire team

### **Authentic Simulation**
- **Real dispensary workflow** - Behind-counter service
- **Business economics** - Wholesale buying, retail selling
- **Customer service** - Realistic interaction patterns
- **Time pressure** - Customer patience creates urgency

### **Scalable Design**
- **Modular systems** - Easy to add new features
- **Event-driven** - Loose coupling between systems
- **Multiplayer-first** - All systems designed for networking
- **Designer-friendly** - ScriptableObjects for easy content creation

---

## 🤝 **Development Status**

**Current State:** ✅ **Multiplayer Foundation Complete**
- Networking infrastructure working
- Core systems implemented  
- UI management polished
- Money system fully functional
- Ready for customer AI implementation

**Time Investment:** ~2-3 weeks of development
**Stability:** High - core systems are robust
**Readiness:** Ready for next major feature (customer system)

---

## 📝 **Notes for Future Development**

### **Code Quality**
- **Well-organized** namespace structure
- **Multiplayer-ready** from ground up
- **Event-driven** architecture for clean separation
- **Commented and documented** key systems

### **Asset Integration**
- **Professional assets** already integrated
- **Consistent art style** established
- **Audio framework** ready for expansion

### **Multiplayer Robustness**
- **Unity Relay** provides stable internet multiplayer
- **Server authority** prevents cheating
- **Graceful disconnection** handling
- **Cross-platform** compatible (build + editor testing)

This README represents the complete current state of the Dispensary Simulator project as of the multiplayer networking implementation phase.