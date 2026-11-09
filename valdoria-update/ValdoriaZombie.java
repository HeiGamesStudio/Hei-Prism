package com.valdoria.zumbi;

import net.minecraft.server.level.ServerLevel;
import net.minecraft.world.entity.EntityType;
import net.minecraft.world.entity.ai.goal.FloatGoal;
import net.minecraft.world.entity.ai.goal.LookAtPlayerGoal;
import net.minecraft.world.entity.ai.goal.RandomLookAroundGoal;
import net.minecraft.world.entity.ai.goal.RandomStrollGoal;
import net.minecraft.world.entity.ai.memory.MemoryModuleType;
import net.minecraft.world.entity.monster.zombie.Zombie;
import net.minecraft.world.entity.npc.Villager;
import net.minecraft.world.level.Level;

/**
 * Zumbi da Valdoria que assusta aldeões, mas não ataca jogadores nem aldeões.
 *
 * A entidade conserva o mesmo ID valdoria_zumbi:zumbi usado na versão anterior,
 * para manter os zumbis já salvos no mundo ao atualizar o JAR.
 */
public final class ValdoriaZombie extends Zombie {
    private static final double FEAR_RADIUS = 8.0D;
    private static final double FEAR_RADIUS_SQUARED = FEAR_RADIUS * FEAR_RADIUS;

    public ValdoriaZombie(EntityType<? extends Zombie> type, Level level) {
        super(type, level);
        setPersistenceRequired();
        setInvulnerable(true);
    }

    /**
     * Não registrar metas de perseguição, ataque ou vingança.
     * Só passear e olhar para os aldeões.
     */
    @Override
    protected void registerGoals() {
        this.goalSelector.addGoal(1, new FloatGoal(this));
        this.goalSelector.addGoal(5, new RandomStrollGoal(this, 0.85D));
        this.goalSelector.addGoal(6, new LookAtPlayerGoal(this, Villager.class, 10.0F));
        this.goalSelector.addGoal(7, new RandomLookAroundGoal(this));
    }

    /** Não desaparecer por distância ou pelo modo Pacífico. */
    @Override
    public void checkDespawn() {
        // Não faz nada: zumbis especiais só saem via /zumbi limpar.
    }

    @Override
    public void tick() {
        super.tick();
        if (!(level() instanceof ServerLevel level) || !isAlive()) return;

        // Também converte zumbis salvos da versão anterior para o novo comportamento.
        if (!isPersistenceRequired()) setPersistenceRequired();
        if (!isInvulnerable()) setInvulnerable(true);
        setTarget(null);
        setAggressive(false);

        // O sensor vanilla usa IDs exatos e pode ignorar o tipo de entidade personalizado.
        // Alimentar somente a memória de perigo dos aldeões que veem ESTE zumbi.
        // O próprio Brain do aldeão controla fuga, pânico e invocação de golens.
        if (tickCount % 2 != 0) return;
        for (Villager villager : level.getEntitiesOfClass(
                Villager.class, getBoundingBox().inflate(FEAR_RADIUS),
                v -> v.isAlive() && distanceToSqr(v) <= FEAR_RADIUS_SQUARED && hasLineOfSight(v))) {
            villager.getBrain().setMemory(MemoryModuleType.NEAREST_HOSTILE, this);
        }
    }
}
