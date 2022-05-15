# Multi-Agent Aircraft Boarding Simulation

Unity simulation of multiple airplane boarding methods. 
Controllable parameters include:

* Plane boarding method (Random, Front-to-Back, Back-to-Front, Window-Middle-Aisle, Steffen's Perfect, and Steffen's Modified).
* Min Stowage Time - Minimum time a passenger will spend stowing their luggage.
* Max Stowage Time - Maximum time a passenger will spend stowing their luggage.
* Num Rows to Board (Only applies to front-to-back and back-to-front) - Chooses how many rows to call up at once.
* Wait Time - How much time to wait between calling each passenger (scaled to the number of passengers).
* Simulation Speed - Time scale to run the simulation (5 is a good speed, set speed to 1 for a true plane boarding experience).
* Number of Trials - How many times to run the selected boarding method.

To run the simulations:

1. Clone this repository with `https://github.com/jsicheng/airplane-boarding-simulation.git`
2. Navigate into the `executable` folder.
3. Open `cmd` and run `plane_boarding.exe`.
