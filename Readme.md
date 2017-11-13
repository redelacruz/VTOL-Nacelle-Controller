######Deployed with MDK
https://github.com/malware-dev/MDK-SE

#VTOL Nacelle Controller
*An in-game script for Space Engineers*

VTOL Nacelle Controller manages and controls the rotors that turn the nacelles on vertical takeoff and landing ships and vehicles. Individual rotors are configured using customizable strings saved in their respective Custom Data fields. Intended for use alongside [Whip's Rotor Thruster Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=757123653).

VTOL Nacelle Controller supports angle and velocity limits, offsets for mismatched rotors (rotors with zero-degree angles facing in different directions), and mirrored rotors. It supports two movement modes based on a reference rotor:
* Follow and try to match the angle of the reference rotor controlled by the user
* Match the properties of a reference rotor controlled by the script

Aside from managing direction, the script also automatically engages safety locks to prevent unwanted torque. This behavior currently cannot be controlled, however, this feature is planned.

Other features that are also planned or may be included in the future:
* Smoothing and acceleration for fast, yet smooth transitions (based on tested code from an old project)
* Multiple reference rotors (doable, just can't think of a reason why yet)
* Proportional control (angle and velocity as percentage of reference rotor's movement)
* New, smoother dampening for off-grid thrusters than Whip's Rotor Thruster Manager currently provides

### WARNING
This script (on GitHub) is considered a work in progress. Some functions may not even work outright. Release versions will be published on the Steam Workshop when they are ready. I take no responsibility for Rapid Unplanned Disassemblies that occur as a result of using work-in-progress code. Use at your own risk.