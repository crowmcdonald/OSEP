


List info about all namespaces
`lsns`

If `mnt` matches the host, the container can access the host filesystem. Try mounting `/` or inspecting files:
	ls /host/root
	
If the `uts` or `user` namespaces are shared, you might affect host settings like the hostname.