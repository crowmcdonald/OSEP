

Credentials


| User          | Machine | Password | NTLM                             | SHA1                                     | DPAPI                            |     |
| ------------- | ------- | -------- | -------------------------------- | ---------------------------------------- | -------------------------------- | --- |
| Administrator | Web05   |          | 9689cee5c72d2ef437de593af89bb4ff | 72ddb87cab50eaa88d2336d2b61a0b4da2b7258f |                                  |     |
| WEB05$        | Web05   |          | 2302158c61440fd7dde505a4be3f9bcb | e93bad583a31d1d62657a9401eabf66b979feead |                                  |     |
| WEB05$        | Web05   |          | 426a605743b34cc258d307598ad3496a | 2327f7ff65cdf442d4a2a61353201d19ff8d80e6 |                                  |     |
| adminWebSvc   | Web05   |          | b0df1cb0819ca0b7d476d4c868175b94 | 030ad1e5ed2598ee743a0e7e3384ce07de5b93e6 | 8ed97d67c65570246e963f53f00fc060 |     |
|               |         |          |                                  |                                          |                                  |     |
|               |         |          |                                  |                                          |                                  |     |


| Machine         | Hostname | Flag                             |
| --------------- | -------- | -------------------------------- |
| 172.16.158.180  |          |                                  |
| 172.16.158.192  |          |                                  |
| 172.16.158.188  |          |                                  |
| 172.16.158.187  |          |                                  |
| 172.16.158.184  |          |                                  |
| 172.16.158.183  |          |                                  |
| 192.168.158.181 | Web05    | 2c81447ea681c098fb2b1874f83f8b47 |
| 172.16.158.194  |          |                                  |
| 172.16.158.197  |          |                                  |
Below are the proof guide

local proof
web06    no    yes
web05    yes    yes
sql11    no    yes
sql03    yes    yes
jump03    yes    yes
dc02    no    yes
dc01    no    yes
appserver05    no    yes
ansible06    yes    yes