namespace Pragsys.CQRS;

public interface IBaseRequest { }

public interface IRequest : IBaseRequest { }

public interface IRequest<TResult> : IBaseRequest { }
