using System;
using System.Windows.Input;

namespace KaosViewModel
{
    // No parameter version.
    public class RelayCommand : ICommand
    {
        private readonly Action action;
        private readonly Predicate<object> predicate;

        public RelayCommand (Action action) : this (action, null)
        { }

        public RelayCommand (Action action, Predicate<object> predicate)
        {
            if (action == null)
                throw new ArgumentNullException (nameof (action));
            this.action = action;
            this.predicate = predicate;
        }

        // parameter is always null.
        public bool CanExecute (object parameter)
         => predicate == null || predicate (parameter);

        public void Execute (object parameter)
         => action();

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }


    // Single parameter version.
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> action;
        private readonly Predicate<T> predicate;

        public RelayCommand (Action<T> action) : this (action, null)
         => this.action = action;

        public RelayCommand (Action<T> action, Predicate<T> predicate)
        {
            if (action == null)
                throw new ArgumentNullException (nameof (action));
            this.action = action;
            this.predicate = predicate;
        }

        public bool CanExecute (object parameter)
         => predicate == null || predicate ((T) parameter);

        public void Execute (object parameter)
         => this.action ((T) parameter);

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
