namespace Pragsys.CQRS;

public delegate Task RequestHandlerDelegate();

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();
