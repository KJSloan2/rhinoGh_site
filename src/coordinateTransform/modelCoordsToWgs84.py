import os
import json
import math
import numpy as np
import csv
######################################################################################
def latlon_to_local_meters(lat, lon, lat0, lon0):
    """
    Local tangent-plane approximation (equirectangular).
    Returns (E, N) meters relative to (lat0, lon0).
    """
    R = 6378137.0  # Earth radius (WGS84)
    dlat = math.radians(lat - lat0)
    dlon = math.radians(lon - lon0)
    lat0r = math.radians(lat0)

    E = R * dlon * math.cos(lat0r)
    N = R * dlat
    return (E, N)
######################################################################################
def project_real_points_to_local_EN(realRefPoints):
    """
    realRefPoints: list of (lat, lon) tuples (same order as modelRefPoints)
    """
    lat0 = sum(p[0] for p in realRefPoints) / float(len(realRefPoints))
    lon0 = sum(p[1] for p in realRefPoints) / float(len(realRefPoints))
    EN = [latlon_to_local_meters(lat, lon, lat0, lon0) for (lat, lon) in realRefPoints]
    return EN, (lat0, lon0)
######################################################################################
def fit_similarity_2d(model_xy, world_xy):
    P = np.array(model_xy, dtype=float)
    Q = np.array(world_xy, dtype=float)

    cp = P.mean(axis=0)
    cq = Q.mean(axis=0)

    X = P - cp
    Y = Q - cq
    H = X.T @ Y
    U, S, Vt = np.linalg.svd(H)
    R = Vt.T @ U.T

    # reflection guard
    if np.linalg.det(R) < 0:
        Vt[-1, :] *= -1
        R = Vt.T @ U.T

    s = S.sum() / (X**2).sum()
    t = cq - s * (R @ cp)
    return s, R, t
######################################################################################
def apply_similarity(s, R, t, x, y):
    v = np.array([x, y], dtype=float)
    X, Y = s * (R @ v) + t
    return float(X), float(Y)
######################################################################################
def rms_error(model_xy, world_xy, s, R, t):
    errs = []
    for (x, y), (X, Y) in zip(model_xy, world_xy):
        Xp, Yp = apply_similarity(s, R, t, x, y)
        errs.append((Xp - X)**2 + (Yp - Y)**2)
    return float(np.sqrt(np.mean(errs)))
######################################################################################
def best_ordered_similarity(model_xy, world_xy):
    """
    Tests cyclic shifts and reversal on world_xy to find best correspondence.
    Returns best (s, R, t, world_xy_best, meta)
    """
    n = len(world_xy)
    best = None

    def shifts(seq):
        for k in range(n):
            yield k, seq[k:] + seq[:k]

    for reversed_flag in (False, True):
        seq = list(world_xy)[::-1] if reversed_flag else list(world_xy)

        for k, candidate in shifts(seq):
            s, R, t = fit_similarity_2d(model_xy, candidate)
            e = rms_error(model_xy, candidate, s, R, t)

            if (best is None) or (e < best["rms"]):
                best = {
                    "rms": e,
                    "s": s,
                    "R": R,
                    "t": t,
                    "world_xy_best": candidate,
                    "reversed": reversed_flag,
                    "shift": k
                }

    return best["s"], best["R"], best["t"], best["world_xy_best"], best
######################################################################################
modelRefPoints_path = os.path.join("data", "geoRef", "modelRefPoints.json")
with open(modelRefPoints_path, 'r') as modelRefFile:
    modelRefJson = json.load(modelRefFile)
modelRefPoints = []
coords = modelRefJson["features"][0]["geometry"]["coordinates"][0]
if coords[0] == coords[-1]:
    coords = coords[:-1]
for coord in coords:
    modelRefPoints.append((coord[0], coord[1]))

realRefPoints_path = os.path.join("data", "geoRef", "realRefPoints.json")
with open(realRefPoints_path, 'r') as realRefFile:
    realRefJson = json.load(realRefFile)
realRefPoints = []
coords = realRefJson["features"][0]["geometry"]["coordinates"][0]
if coords[0] == coords[-1]:
    coords = coords[:-1]
for coord in coords:
    realRefPoints.append((coord[1], coord[0]))
#realRefPoints = list(reversed(realRefPoints))

world_EN, (lat0, lon0) = project_real_points_to_local_EN(realRefPoints)
s, R, t, world_EN_best, meta = best_ordered_similarity(modelRefPoints, world_EN)
#a, b, c, d, tx, ty = fit_similarity_2d(modelRefPoints, world_EN)

# Read and update agent path collisions csv
agentCollisionsCsv_path = os.path.join("data", "agent_paths", "agent_path_collisions.csv")
with open(agentCollisionsCsv_path, 'r') as csvFile:
    csvReader = csv.DictReader(csvFile)
    collisions = [row for row in csvReader]
    fieldnames = csvReader.fieldnames + ['lat', 'lon']
######################################################################################
# Transform coordinates and add lat/lon to each row
for collision in collisions:
    x_model = float(collision["x"])
    y_model = float(collision["y"])
    X_world, Y_world = apply_similarity(s, R, t, x_model, y_model)
    
    # Convert back to lat/lon
    dlat = Y_world / 6378137.0 * (180.0 / math.pi)
    dlon = X_world / (6378137.0 * math.cos(math.radians(lat0))) * (180.0 / math.pi)
    lat = lat0 + dlat
    lon = lon0 + dlon
    
    # Add lat/lon to the row dictionary
    collision['lat'] = lat
    collision['lon'] = lon
######################################################################################
# Write updated data back to CSV
with open(agentCollisionsCsv_path, 'w', newline='') as csvFile:
    csvWriter = csv.DictWriter(csvFile, fieldnames=fieldnames)
    csvWriter.writeheader()
    csvWriter.writerows(collisions)

print(f"Updated CSV with lat/lon coordinates: {agentCollisionsCsv_path}")