using UnityEngine;

// Een enkele virtuele zombie met positie, doel en status
public class VirtualZombie
{
    public int id;                     // Uniek ID voor debug/doeleinden
    public Vector3 startPos;           // Startpositie (rand van de map)
    public Vector3 destPos;            // Doelpositie (tegenoverliggende rand)
    public Vector3 currentPos;         // Huidige positie in de virtuele wereld
    public bool isSpawned = false;     // True als momenteel als Entity in de wereld aanwezig
    public int spawnedEntityId = -1;   // EntityId van de gespawnede entity (indien isSpawned)
    public float moveStep = 0.1f;      // Bewegingsincrement per update (in wereldblok eenheden)

    public VirtualZombie(int id, Vector3 start, Vector3 dest)
    {
        this.id = id;
        this.startPos = start;
        this.destPos = dest;
        this.currentPos = start;
    }

    // Check of deze zombie (virtueel) zijn bestemming bereikt heeft
    public bool ReachedDestination()
    {
        // Beschouw doel bereikt als binnen 5 meter van destPos
        float distSq = (currentPos.x - destPos.x) * (currentPos.x - destPos.x)
                     + (currentPos.z - destPos.z) * (currentPos.z - destPos.z);
        return distSq < 25f;
    }
}
