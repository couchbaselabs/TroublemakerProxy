# DisconnectionPlugin

This configuration has two entries.  `PatternClauses` contains an array of pattern strings which will be applied as logical `AND` to each other.  `DisconnectType` specifies what kind of disconnect should occur.  Refer to the [sample_config.json](sample_config.json) file for layout.

## PatternClauses key

A list of one or more patterns that must be matched before the disconnect will take effect.  The grammar for the patterns is as follows (The pattern to consider is `blip_comparison`):

```
; Lexer tokens
Integer: [0-9]+
Equal: =|==
After: "after"
Before: "before"
Minutes: "minutes"|"minute"
Seconds: "seconds"|"second"
Milliseconds: "milliseconds"|"millisecond"
BlipTypeRequest: "request"|"msg"
BlipTypeResponse: "response"|"rpy"
BlipTypeError: "error"|"err"
BlipType: "type"
BlipMsgNo: "msgno"|"num"

; Parse Rules
blip_msg_type: BlipTypeRequest
blip_msg_type: BlipTypeResponse
blip_msg_type: BlipTypeError

time_part: Minutes
time_part: Seconds
time_part: Milliseconds

time: Integer time_part

; Start with these four, see above for definitions
blip_comparison: Before time
blip_comparison: After time
blip_comparison: BlipMsgNo Equal Integer
blip_comparison: BlipType Equal blip_msg_type
```

Examples:
- After 5 seconds
- msgno = 3
- type == rpy

## DisconnectType

An enum representing how the proxy should disconnect from the client.  

| Name                | Action |
| ------------------- | ------ |
| BLIPErrorMessage    | Sends back a BLIP message with an error code 500 |
| WebSocketClose      | Closes the web socket connection with a 1002 code |
| PipeBreak (default) | Breaks the TCP socket connection without any message |
| Timeout             | Doesn't send a response for 2 minutes, triggering a client timeout |