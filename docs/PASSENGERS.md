# Passenger personalities

Every rider who boards has a personality (`Scripts/Logic/Riders.cs`, rules in
`PassengerRoute.Serve`, tests in `Tests/EditMode/RiderTests.cs`). Most are ordinary; the rest:

| Rider | Tag on the HUD | What they do |
|---|---|---|
| Estudyante | ESTUDYANTE | Student discount (80 % fare); mostly daytime |
| Lola / Lolo | LOLA | Smooth ride = +3 tip. A crash or a fast hump/pothole upsets her (no tip) |
| Chismosa | CHISMOSA | Gossips the whole way; pays normally |
| Nagmamadali | NAGMAMADALI 12s | Deadline countdown: on time = +4 tip, late = complaint and −0.15 rating |
| Lasing | LASING | Night only; 25 % chance to pay double ("Sa'yo na ang sukli!") |
| Hindi nagbayad | – (looks like anyone) | Jumps off without paying: honk (BUSINA) within 2.5 s to make them pay |
| Tindera | TINDERA | Big bayong takes two seats; +3 for the cargo |
| Balikbayan | BALIKBAYAN | Rare (one at a time); +10 tip if no crash while aboard |

Chances depend on the route (more people in a hurry in the city, more tinderas in towns) and the
time of day. Tips scale with the driver rating / passenger bonus, not the delivery streak.
Results show the tips and the fare dodgers caught.

Personalities come from their own seeded stream, so the Daily Challenge road, stop names and
destinations are unchanged. Tips fit inside the server's coin bound for a run (see the test
`TipsStayWithinTheServerCoinBound`), so no server change is needed.

Riders still use the existing person models (no new art yet): personalities show through the
HUD tags, speech bubbles and messages.
