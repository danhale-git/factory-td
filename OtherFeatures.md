# Production and logistics

## *Production buildings also act as conveyor belts*

* Each production building has two **cranes** attached, one for input and one for output.

* **Cranes** will collect or place items for the production building.

* Each **crane** has a fixed **grid area** where the player can choose a collect or place position.

* Production buidings are chained together, with input and output **grid area** overlapping.

Logic puzzles emerge from the need to place buildings of varying size, processing speed, and input/output recipe. Cranes Grid areas will need to overlap in a way that creates balanced, optimal processing and transit of items. The size and shape of crane grid areas is key to encouraging challenging puzzles to emerge.

<p align="center">
<img src="https://imgur.com/hrLkNyy.png">
</p>
<p align="center">
Example of a production building with input and output crane grid areas.
Player designates one green and one red square as locations for the cranes to place and grab.
</p>

### Notes
Sorter building will probably be needed. Should be expensive/slow and have a 1x1 crane grid area. Should only function when multiple resources are input, to prevent players chaining sorters to create a conveyor.

## *Maze tower defence replaces mining*

* Spawners are discovered by the player and output creeps with a consistent rhythm.

* Creeps navigate to a central player structure, only attacking when they reach it or there is no path to it.

* Player builds a grid-based wall maze and places static defences (towers) around it.

* Creeps all drop a resource on death, which is used for production. More common creeps drop more common resources.

* Resources can be collected with production building's crane, or using a bulldozer.

* Bulldozer is a static unit which pushes all resources in a long X-wide line (L shape?) to a single position, for more efficient collection.

### Notes
Bulldozer should block the maze path, requiring the player to re-route creeps or get attacked.
Automatic doors, and timers needed to enable synchronising bulldozer with re-routing.
Doors passable only by players and bulldozers are essential for QOL in maze TD.

# Player movement
