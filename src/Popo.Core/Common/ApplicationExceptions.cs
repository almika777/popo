namespace Popo.Core.Common;

public abstract class ApplicationFailureException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class InvalidApplicationRequestException(string message, Exception? innerException = null)
    : ApplicationFailureException(message, innerException);

public sealed class ApplicationResourceNotFoundException(string message = "Запись не найдена.")
    : ApplicationFailureException(message);

public sealed class ApplicationConflictException(string message)
    : ApplicationFailureException(message);
