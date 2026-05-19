# Level Narrative Nodes

## Plan

This level is structured as seven quiet narrative beats. Each node should be readable through motion, distance, sound, and small prompts rather than exposition. The path begins at the mountain tunnel exit, climbs through a left-side rising platform, passes a rail transfer puzzle, and ends at the great clock tower. The final beat happens after the cart arrives: the clock tower drifts into alignment, making the station feel briefly awake again.

## Node 1: Tunnel Mouth

**Scene Change**  
The cart rolls out from a dark mountain tunnel into open sky. Wind, clouds, and floating stone silhouettes reveal the station ruins ahead.

**Player Action**  
Guide the cart forward and let the view settle on the broken track.

**Line / Prompt**  
"The mountain lets you go."

**Trigger Condition**  
Player/cart exits the tunnel volume.

**Possible Unity Trigger**  
Trigger collider at tunnel exit, short Timeline camera reveal, ambient audio fade, light shaft animation event.

## Node 2: Sleeping Platform

**Scene Change**  
The left platform sits low and silent beside the track. A faint rail mark glows across its edge.

**Player Action**  
Use the nearby mechanism block to wake or raise the left platform.

**Line / Prompt**  
"Lift the old stone."

**Trigger Condition**  
Player enters the left platform puzzle area or aims at the platform mechanism.

**Possible Unity Trigger**  
Interactable trigger, mechanism drag completion, animation event when platform begins rising, subtle UI prompt fade.

## Node 3: Left Platform Rising

**Scene Change**  
The platform climbs into place. Dust falls away. A suspended track segment meets the station line.

**Player Action**  
Hold the platform until its rail lines align, then release or continue the route.

**Line / Prompt**  
"Higher. Until the rails remember."

**Trigger Condition**  
Platform reaches the target height or rail connector distance/angle becomes valid.

**Possible Unity Trigger**  
RailConnector condition pass, Timeline rumble and stone movement, animation event, UnityEvent from mechanism state.

## Node 4: Transfer Yard

**Scene Change**  
Several broken rails face different islands. The middle transfer path waits like a switchboard in the sky.

**Player Action**  
Rotate or slide the rail-control blocks to form a continuous route through the transfer.

**Line / Prompt**  
"Turn the path. Do not wake the fall."

**Trigger Condition**  
Cart reaches the transfer approach or first transfer connector fails/passes.

**Possible Unity Trigger**  
RailPath connector check, interaction button, drag-ended snap event, blocked-at-end UnityEvent.

## Node 5: Station Crossing

**Scene Change**  
The cart passes from one island segment to another. Distant signs, gates, and old stop markers line the route.

**Player Action**  
Drive through the newly joined track and adjust any final crossing piece.

**Line / Prompt**  
"The station answers in pieces."

**Trigger Condition**  
Cart changes RailPath after a valid transfer connection.

**Possible Unity Trigger**  
TrainOnRails `onChangedPath`, connector transition points, short teleport-step effect, audio cue at each rail join.

## Node 6: Clock Tower Approach

**Scene Change**  
The great clock tower rises ahead as the final station. Its bell frame is still, slightly misaligned with the landing track.

**Player Action**  
Guide the cart along the last rail and solve the final alignment if needed.

**Line / Prompt**  
"The last tower waits without time."

**Trigger Condition**  
Cart enters the final approach path or reaches the last connector before the tower.

**Possible Unity Trigger**  
Trigger collider near tower approach, Timeline camera tilt toward tower, RailConnector custom angle condition, ambient bell layer fade in.

## Node 7: Bell Alignment

**Scene Change**  
The cart reaches the end. The clock tower floats, turns, and settles into alignment with the terminal platform. The bell gives one low sound.

**Player Action**  
Arrive at the final stop and remain as the tower completes its movement.

**Line / Prompt**  
"Now the station knows where it stands."

**Trigger Condition**  
Train reaches the final RailPath end or fires `onReachedFinalEnd`.

**Possible Unity Trigger**  
TrainOnRails `onReachedFinalEnd`, Timeline tower drift, animation event for final lock, bell audio one-shot, optional camera hold.
