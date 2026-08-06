export async function loadRemoteStatus(endpoint) {
  const response = await fetch(endpoint);
  const payload = await response.json();
  return payload.status;
}
