n_levels_per_world = [13, 13, 11, 13, 12]
ls = []
for i in range(1, 6):
    for k in range(1, n_levels_per_world[i - 1] + 1):
       ls.append((i, k))

def formatClam(l):
    (w, c) = l
    return f"{w}:{c:02d}"


print(". -> 1:01")
for i in range(0, len(ls) - 1):
    l = ls[i]
    r = ls[i + 1]
    l_str = formatClam(l)
    r_str = formatClam(r)
    print(l_str)
    print(f"{l_str} -> {r_str}")

print("5:12")
print("Final Boss");
