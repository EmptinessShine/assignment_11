using System;
using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour
{
    // set from the stats asset in Init(), not in the Inspector
    [NonSerialized] public int maxHealth;
    [NonSerialized] public int currentHealth;

    [SerializeField] SpriteRenderer body;
    [SerializeField] GameObject hitEffect;
    [SerializeField] GameObject deathEffect;
    [SerializeField] AudioClip hurtSound;
    [SerializeField] AudioClip deathSound;

    public event Action Damaged;
    public event Action Died;

    float invincibleTime;
    float invincibleTimer;

    // the player and the enemies call this with numbers from their stats
    public void Init(int health, float invincible)
    {
        maxHealth = health;
        currentHealth = health;
        invincibleTime = invincible;
    }

    void Update()
    {
        invincibleTimer -= Time.deltaTime;
    }

    public void TakeDamage(int damage)
    {
        if (currentHealth <= 0 || invincibleTimer > 0) return;

        currentHealth -= damage;
        invincibleTimer = invincibleTime;

        if (hitEffect != null) Instantiate(hitEffect, transform.position, Quaternion.identity);
        if (body != null) StartCoroutine(Flash());

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            if (deathEffect != null) Instantiate(deathEffect, transform.position, Quaternion.identity);
            Sound.Play(deathSound);
            if (Died != null) Died();
        }
        else
        {
            Sound.Play(hurtSound);
            if (Damaged != null) Damaged();
        }
    }

    public void Heal(int amount)
    {
        if (currentHealth <= 0) return;
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
    }

    IEnumerator Flash()
    {
        body.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (currentHealth > 0) body.color = Color.white;
    }
}
