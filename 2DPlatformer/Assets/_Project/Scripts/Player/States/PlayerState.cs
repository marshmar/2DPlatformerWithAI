namespace Platformer
{
    /// <summary>
    /// 플레이어 상태. 상태마다 물리 처리 방식이 달라지므로(지상/공중/벽/갈고리 스윙…) 상태가 FixedUpdate 를 맡는다.
    /// 애니메이션 상태와는 별개 — 예를 들어 Idle/Run 은 둘 다 GroundedState 다.
    /// </summary>
    public abstract class PlayerState
    {
        protected readonly PlayerController Player;

        protected PlayerState(PlayerController player) => Player = player;

        public virtual void Enter() { }
        public virtual void Exit() { }

        /// <summary>이동 처리 후 다른 상태로 넘어갈 조건을 검사한다.</summary>
        public abstract void FixedTick();
    }
}
