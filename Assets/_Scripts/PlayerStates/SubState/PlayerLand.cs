using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerLand : AirState
{
    public PlayerLand(Player player, PlayerStateMachine stateMachine, PlayerData playerData, PlayerAnimationHolder playerAnimation) : base(player, stateMachine, playerData, playerAnimation)
    {

    }

    private float landingDuration;
    private bool isLandingDelay = false;

    public override void Enter()
    {
        base.Enter();

        player.SetLandingEvent();
        player.ResetJumpCount();
        player.SetVelocityY(groundedVerticalVelocity);


        player.Anim.SetFloat("falling", 0);
        player.Anim.Play(playerAnimation.landTrigger);

        if(stateMachine.LastState == player.AirNormalAttackState)
        {
            landingDuration = playerData.recoveryAirNormalAttack;
            isLandingDelay = true;            
        }
        else
        {
            landingDuration = playerData.recoveryFall;
            isLandingDelay = true;
        }
                 
    }

    public override void Exit()
    {
        base.Exit();
        
        landingDuration = 0;
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

        if (landingDuration > 0)
        {
            if (onJumpPressedTolerance)
            {

                if (isTurning)
                {
                    isTurning = false;
                    ForceTurning();
                }

                stateMachine.ChangeState(player.JumpState);
            }

            else
                landingDuration -= Time.deltaTime;

        }

        else if(landingDuration <=0)
        {
            isLandingDelay = false;
            FinishLanding();
        }

    }

    public override void PhysicsUpdate()
    {
        base.PhysicsUpdate();
    }

    private void FinishLanding()
    {
        isLandingDelay = false;

        

        if (Mathf.Abs(player.PlayerVelocity.x) <= 0.1f)
        {
            stateMachine.ChangeState(player.IdleState);
        }
        else if (Mathf.Abs(player.PlayerVelocity.x) > 0.1f)
        {
            stateMachine.ChangeState(player.MoveState);
        }
    }
}
