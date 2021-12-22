#100=-0.01 (Min X Travel)
#101=-17.99 (Max X Travel)
#102=-0.01 (Min Y Travel)
#103=-17.99 (Max Y Travel)
#104=-5.0 (Min Z Travel)
#105=-0.01 (Max Z Travel)

( Amount of dwell after axis travel. )
( Adjust so total time at each spindle rpm )
( is about 200 seconds )
#106=200

( Safe Starting Conditions )
G0 G40 G49 G50 G80 G94 G17 G20 G40 G49 G54 G64 G80 G90 G98 M05

( Alternate spindle speed with axis warmup. )
( Adjust for your spindle’s speed ranges. )
G28
S1000 M3
M98 P2000 (Warmup axes)
S2000
M98 P2000 (Warmup axes)
S4000
M98 P2000 (Warmup axes)
S8000
M98 P2000 (Warmup axes)
S16000
M98 P2000 (Warmup axes)
S24000
M98 P2000 (Warmup axes)
S30000
M98 P2000 (Warmup axes)

M5 G28

M30 (End of program)

O2000 (Axis warm up subprogram)
G28
F50 G01
Z#104 (Do Z first and leave Z parked high for the rest )
Z#105
X#101
Y#103
X#100
Y#102
G04 P#106
M99 (Return from subprogram)