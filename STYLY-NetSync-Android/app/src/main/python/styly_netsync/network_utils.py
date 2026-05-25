"""Network utility functions for STYLY NetSync."""

import logging
import socket

try:
    import psutil
except Exception:  # pragma: no cover - Android may not provide psutil wheels
    psutil = None

logger = logging.getLogger(__name__)


def get_local_ip_addresses() -> list[str]:
    """
    Get all local IP addresses of the machine from physical network interfaces.

    Filters out virtual interfaces (bridges, VPNs, Docker, etc.) and APIPA addresses
    (169.254.x.x) to show only IP addresses that are likely accessible from external devices.

    Returns:
        list: List of IP addresses as strings

    Example:
        >>> ips = get_local_ip_addresses()
        >>> print(ips)
        ['192.168.1.100', '10.0.0.50']
    """
    ip_addresses = []
    try:
        # psutil path: richer interface filtering when available.
        if psutil is not None:
            # Patterns to exclude virtual/bridge interfaces
            # These are common virtual interface prefixes across different platforms
            virtual_prefixes = (
                "bridge",  # VMware, Parallels bridges
                "docker",  # Docker interfaces
                "veth",  # Virtual Ethernet (Docker, LXC)
                "vmnet",  # VMware network
                "vboxnet",  # VirtualBox network
                "virbr",  # libvirt bridge
                "tun",  # VPN tunnels
                "tap",  # Virtual network tap
                "utun",  # macOS VPN tunnels
                "vnic",  # Virtual NIC
                "ppp",  # Point-to-Point Protocol (VPN)
            )

            # Get all network interfaces
            for interface_name, interface_addresses in psutil.net_if_addrs().items():
                # Skip virtual interfaces
                if interface_name.lower().startswith(virtual_prefixes):
                    continue

                for address in interface_addresses:
                    # Filter for IPv4 addresses only
                    if address.family == socket.AF_INET:
                        ip = address.address
                        # Exclude localhost and APIPA addresses (169.254.x.x)
                        if ip != "127.0.0.1" and not ip.startswith("169.254."):
                            ip_addresses.append(ip)

            return ip_addresses

        # Fallback path for environments without psutil (e.g., Android).
        # Prefer a route-based local address and then add hostname-resolved addresses.
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
                s.connect(("8.8.8.8", 80))
                route_ip = s.getsockname()[0]
                if route_ip != "127.0.0.1" and not route_ip.startswith("169.254."):
                    ip_addresses.append(route_ip)
        except Exception:
            pass

        try:
            _, _, host_ips = socket.gethostbyname_ex(socket.gethostname())
            for ip in host_ips:
                if ip != "127.0.0.1" and not ip.startswith("169.254.") and ip not in ip_addresses:
                    ip_addresses.append(ip)
        except Exception:
            pass

        if psutil is None:
            logger.info("psutil unavailable; using socket-based IP discovery")
        return ip_addresses

    except Exception as e:
        logger.warning(f"Failed to get local IP addresses: {e}")

    return ip_addresses
